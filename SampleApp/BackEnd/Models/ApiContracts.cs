namespace BackEnd.Models;

public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string BadRequest = "BAD_REQUEST";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string InternalError = "INTERNAL_ERROR";
}

public sealed record ApiError(string Code, string Message, object? Details = null);
public sealed record ApiErrorResponse(ApiError Error);
public sealed record ServiceSummary(string Id, string Name, string Description, string[] ServiceAreas);
public sealed record ServiceListResponse(ServiceSummary[] Items);
public sealed record AppointmentCreatedResponse(
    int Id, string Reference, string Status, DateTimeOffset CreatedAt, string ConfirmationMessage);
public sealed record ReviewCreatedResponse(int Id, string[] PhotoUrls, DateTimeOffset CreatedAt);
public sealed record HealthResponse(string Status, IReadOnlyDictionary<string, string> Services);

public sealed class CreateAppointmentInput
{
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string PreferredLanguage { get; set; } = "es-US";
    public bool SmsConsent { get; set; }
    public string ServiceId { get; set; } = "";
    public int? PalmTreeCount { get; set; }
    public int? PlantCount { get; set; }
    public string Address { get; set; } = "";
    public DateTime ServiceDateTimeLocal { get; set; }
    public string? Notes { get; set; }
}

public sealed class CreateReviewInput
{
    public string Name { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public int Rating { get; set; }
    public string Comment { get; set; } = "";
    public bool PhotoPublicConsent { get; set; }
}