using BackEnd.Models;
using FluentValidation;

namespace BackEnd.Validation;

public sealed class CreateAppointmentValidator : AbstractValidator<CreateAppointmentInput>
{
    private static readonly HashSet<string> ServiceIds = ["palm-trees", "planting", "sprinkler-repair", "outdoor-maintenance"];

    public CreateAppointmentValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Phone).NotEmpty().MaximumLength(32);
        RuleFor(request => request.PreferredLanguage).Must(value => value is "es-US" or "en-US");
        RuleFor(request => request.SmsConsent).Equal(true);
        RuleFor(request => request.ServiceId).Must(ServiceIds.Contains);
        RuleFor(request => request.Address).NotEmpty().MaximumLength(300);
        RuleFor(request => request.Notes).MaximumLength(2000);
        RuleFor(request => request.PalmTreeCount).NotEmpty().GreaterThan(0)
            .When(request => request.ServiceId == "palm-trees");
        RuleFor(request => request.PlantCount).NotEmpty().GreaterThan(0)
            .When(request => request.ServiceId == "planting");
        RuleFor(request => request.ServiceDateTimeLocal).Must(IsValidServiceDateTime);
    }

    private static bool IsValidServiceDateTime(DateTime value)
    {
        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ServiceTimeZone()).DateTime;
        return value >= localNow.AddHours(24)
            && value.TimeOfDay >= TimeSpan.FromHours(16)
            && value.TimeOfDay <= TimeSpan.FromHours(20);
    }

    internal static TimeZoneInfo ServiceTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"); }
    }
}

public sealed class CreateReviewValidator : AbstractValidator<CreateReviewInput>
{
    public CreateReviewValidator()
    {
        RuleFor(review => review.Name).NotEmpty().MaximumLength(120);
        RuleFor(review => review.ServiceName).NotEmpty().MaximumLength(120);
        RuleFor(review => review.Rating).InclusiveBetween(1, 5);
        RuleFor(review => review.Comment).NotEmpty().MaximumLength(3000);
    }
}