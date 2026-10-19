using Microsoft.AspNetCore.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    // current workaround for port forwarding in codespaces
    // https://github.com/dotnet/aspnetcore/issues/57332
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Servers = [];
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

        app.MapGet("/api/health", async (AppDbContext db, IPhotoStorageService storage,
            ISmsNotificationService sms, CancellationToken cancellationToken) =>
        {
            var databaseHealthy = false;
            try { databaseHealthy = await db.Database.CanConnectAsync(cancellationToken); }
            catch (Exception exception) { app.Logger.LogWarning(exception, "SQL health check failed"); }

            var statuses = new Dictionary<string, string>
            {
                ["sql"] = databaseHealthy ? "healthy" : "unhealthy",
                ["blobStorage"] = await storage.HealthCheckAsync(cancellationToken) ? "healthy" : "unhealthy",
                ["sms"] = await sms.HealthCheckAsync(cancellationToken) ? "healthy" : "unhealthy"
            };
            var healthyCount = statuses.Values.Count(value => value == "healthy");
            var status = healthyCount == statuses.Count ? "healthy" : healthyCount == 0 ? "unhealthy" : "degraded";
            return Results.Json(new HealthResponse(status, statuses), statusCode:
                status == "unhealthy" ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK);
        }).WithName("HealthCheck");

        app.MapGet("/api/services", () => Results.Ok(new ServiceListResponse(
        [
            new("palm-trees", "Palm trees", "Quote after photo and count review", ServiceAreas),
            new("planting", "Planting", "Plant anything requested", ServiceAreas),
            new("sprinkler-system-repair", "Sprinkler system repair", "Repairs only", ServiceAreas),
            new("outside-maintenance", "Outside maintenance", "Leaf trimming, grass mowing, leaf blowing, and other work by request", ServiceAreas)
        ]))).WithName("ListServices");

        app.MapPost("/api/appointments", async (HttpRequest request, AppDbContext db,
            IPhotoStorageService storage, ISmsNotificationService sms,
            IValidator<CreateAppointmentInput> validator, CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new ApiErrorResponse(new ApiError(ErrorCodes.BadRequest, "Expected multipart/form-data.")));

            var form = await request.ReadFormAsync(cancellationToken);
            var input = ParseAppointmentInput(form);
            await validator.ValidateAndThrowAsync(input, cancellationToken);
            var files = form.Files.GetFiles("servicePhotos");
            if (files.Count == 0)
                throw new ValidationException([new FluentValidation.Results.ValidationFailure("servicePhotos", "At least one service photo is required.")]);
            if (files.Count > 10 || files.Any(file => file.Length > 10 * 1024 * 1024))
                throw new ValidationException([new FluentValidation.Results.ValidationFailure("servicePhotos", "Upload at most 10 images, each no larger than 10 MB.")]);

            var uploadedUrls = new List<string>();
            try
            {
                foreach (var file in files)
                    uploadedUrls.Add(await storage.UploadAsync("appointment-photos", file, cancellationToken));

                var appointment = new AppointmentRequest
                {
                    Reference = $"A{Guid.NewGuid():N}"[..7].ToUpperInvariant(),
                    Name = input.Name.Trim(),
                    Phone = input.Phone.Trim(),
                    PreferredLanguage = input.PreferredLanguage,
                    SmsConsent = input.SmsConsent,
                    ServiceId = input.ServiceId,
                    PalmTreeCount = input.PalmTreeCount,
                    PlantCount = input.PlantCount,
                    Address = input.Address.Trim(),
                    ServiceDateTimeLocal = input.ServiceDateTimeLocal,
                    Notes = input.Notes?.Trim(),
                    PhotoUrls = uploadedUrls
                };

                db.AppointmentRequests.Add(appointment);
                await db.SaveChangesAsync(cancellationToken);
                var staffMessage = BuildStaffMessage(appointment, configuration["STAFF_PHONE_NUMBER"] ?? "+17607593131");
                try
                {
                    await sms.SendAsync(configuration["STAFF_PHONE_NUMBER"] ?? "+17607593131", staffMessage, cancellationToken);
                }
                catch (Exception exception)
                {
                    app.Logger.LogCritical(exception, "Appointment {Reference} saved but staff SMS could not be delivered", appointment.Reference);
                }

                var confirmation = input.PreferredLanguage == "es-US"
                    ? "Gracias por solicitar una cita. Nos comunicaremos con usted lo antes posible."
                    : "Thank you for requesting an appointment. We will get back to you as soon as possible.";
                return Results.Created($"/api/appointments/{appointment.Id}", new AppointmentCreatedResponse(
                    appointment.Id, appointment.Reference, appointment.Status, appointment.CreatedAt, confirmation));
            }
            catch
            {
                foreach (var url in uploadedUrls)
                {
                    try { await storage.DeleteAsync("appointment-photos", url, cancellationToken); }
                    catch (Exception exception) { app.Logger.LogWarning(exception, "Unable to clean up an uploaded appointment photo"); }
                }
                throw;
            }
        }).WithName("CreateAppointment");

        app.MapGet("/api/reviews", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var reviews = await db.Reviews.AsNoTracking().OrderByDescending(review => review.CreatedAt).ToListAsync(cancellationToken);
            return Results.Ok(new { items = reviews.Select(review => new
            {
                review.Id,
                review.Name,
                review.ServiceName,
                review.Rating,
                review.Comment,
                photoUrls = review.PhotoPublicConsent ? review.PhotoUrls : [],
                review.CreatedAt
            }) });
        }).WithName("ListReviews");

        app.MapPost("/api/reviews", async (HttpRequest request, AppDbContext db, IPhotoStorageService storage,
            IValidator<CreateReviewInput> validator, CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new ApiErrorResponse(new ApiError(ErrorCodes.BadRequest, "Expected multipart/form-data.")));

            var form = await request.ReadFormAsync(cancellationToken);
            var input = new CreateReviewInput
            {
                Name = form["name"].ToString(),
                ServiceName = form["serviceName"].ToString(),
                Rating = int.TryParse(form["rating"], out var rating) ? rating : 0,
                Comment = form["comment"].ToString(),
                PhotoPublicConsent = bool.TryParse(form["photoPublicConsent"], out var consent) && consent
            };
            await validator.ValidateAndThrowAsync(input, cancellationToken);
            var files = form.Files.GetFiles("reviewPhotos");
            if (files.Count > 10 || files.Any(file => file.Length > 10 * 1024 * 1024))
                throw new ValidationException([new FluentValidation.Results.ValidationFailure("reviewPhotos", "Upload at most 10 images, each no larger than 10 MB.")]);
            if (files.Count > 0 && !input.PhotoPublicConsent)
                throw new ValidationException([new FluentValidation.Results.ValidationFailure("photoPublicConsent", "Photo consent is required when photos are attached.")]);

            var uploadedUrls = new List<string>();
            try
            {
                foreach (var file in files)
                    uploadedUrls.Add(await storage.UploadAsync("review-photos", file, cancellationToken));
                var review = new Review
                {
                    Name = input.Name.Trim(),
                    ServiceName = input.ServiceName,
                    Rating = input.Rating,
                    Comment = input.Comment.Trim(),
                    PhotoPublicConsent = input.PhotoPublicConsent,
                    PhotoUrls = input.PhotoPublicConsent ? uploadedUrls : []
                };
                db.Reviews.Add(review);
                await db.SaveChangesAsync(cancellationToken);
                return Results.Created($"/api/reviews/{review.Id}", new ReviewCreatedResponse(review.Id, review.PhotoUrls.ToArray(), review.CreatedAt));
            }
            catch
            {
                foreach (var url in uploadedUrls)
                {
                    try { await storage.DeleteAsync("review-photos", url, cancellationToken); }
                    catch (Exception exception) { app.Logger.LogWarning(exception, "Unable to clean up an uploaded review photo"); }
                }
                throw;
            }
        }).WithName("CreateReview");

        app.MapPost("/api/webhooks/communication/sms", async (HttpRequest request, StaffSmsCommandService commands,
            CancellationToken cancellationToken) =>
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
            var events = document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.EnumerateArray().ToArray()
                : [document.RootElement.Clone()];
            foreach (var eventItem in events)
            {
                var eventType = GetString(eventItem, "eventType");
                var data = eventItem.TryGetProperty("data", out var eventData) ? eventData : eventItem;
                if (eventType == "Microsoft.EventGrid.SubscriptionValidationEvent" && data.TryGetProperty("validationCode", out var code))
                    return Results.Ok(new { validationResponse = code.GetString() });

                var from = GetString(data, "from");
                var message = GetString(data, "message") ?? GetString(data, "messageBody");
                if (from is not null && message is not null)
                    await commands.ProcessAsync(from, configuration["STAFF_PHONE_NUMBER"] ?? "+17607593131", message, cancellationToken);
            }
            return Results.Ok(new { accepted = true });
        }).WithName("ReceiveStaffSms");

        app.Run();

        static readonly string[] ServiceAreas = ["Vista", "Carlsbad", "Escondido", "Oceanside", "Fallbrook"];

        static string RequireSetting(IConfiguration configuration, string key) =>
            configuration[key] is { Length: > 0 } value
                ? value
                : throw new InvalidOperationException($"Required setting '{key}' is missing.");

        static CreateAppointmentInput ParseAppointmentInput(IFormCollection form)
        {
            var dateTime = DateTime.TryParseExact(
                $"{form["serviceDate"]}T{form["serviceTime"]}", "yyyy-MM-dd'T'HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : DateTime.MinValue;
            return new CreateAppointmentInput
            {
                Name = form["name"].ToString(),
                Phone = form["phone"].ToString(),
                PreferredLanguage = string.IsNullOrWhiteSpace(form["preferredLanguage"]) ? "es-US" : form["preferredLanguage"].ToString(),
                SmsConsent = bool.TryParse(form["smsConsent"], out var consent) && consent,
                ServiceId = form["serviceId"].ToString(),
                PalmTreeCount = int.TryParse(form["palmTreeCount"], out var palmCount) ? palmCount : null,
                PlantCount = int.TryParse(form["plantCount"], out var plantCount) ? plantCount : null,
                Address = form["address"].ToString(),
                ServiceDateTimeLocal = dateTime,
                Notes = form["notes"].ToString()
            };
        }

        static string BuildStaffMessage(AppointmentRequest appointment, string staffPhone)
        {
            var date = appointment.ServiceDateTimeLocal.ToString("MM/dd/yyyy h:mm tt", CultureInfo.InvariantCulture);
            var service = appointment.ServiceId switch
            {
                "palm-trees" => $"Palm trees ({appointment.PalmTreeCount} trees)",
                "planting" => $"Planting ({appointment.PlantCount} plants)",
                "sprinkler-system-repair" => "Sprinkler system repair",
                _ => "Outside maintenance"
            };
            var photos = string.Join("\n", appointment.PhotoUrls);
            return $"New appointment {appointment.Reference}\n{appointment.Name}, {appointment.Phone}\n{service}\n{date}\n{appointment.Address}\nLanguage: {appointment.PreferredLanguage}\nStaff: {staffPhone}\nPhotos:\n{photos}\nReply $<amount> {appointment.Reference}, OK {appointment.Reference}, UNAVAILABLE {appointment.Reference}, DAY {appointment.Reference} MM/DD, or TIME {appointment.Reference} H:MM pm.";
        }

        static string? GetString(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        internal partial class Program;

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
