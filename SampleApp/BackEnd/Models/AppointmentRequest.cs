using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace BackEnd.Models;

public sealed class AppointmentRequest
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";
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
    public string Status { get; set; } = "Pending";
    public decimal? QuotedPrice { get; set; }
    public string PhotoUrlsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [NotMapped]
    public List<string> PhotoUrls
    {
        get => JsonSerializer.Deserialize<List<string>>(PhotoUrlsJson) ?? [];
        set => PhotoUrlsJson = JsonSerializer.Serialize(value);
    }
}