using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace BackEnd.Models;

public sealed class Review
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public int Rating { get; set; }
    public string Comment { get; set; } = "";
    public bool PhotoPublicConsent { get; set; }
    public string PhotoUrlsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [NotMapped]
    public List<string> PhotoUrls
    {
        get => JsonSerializer.Deserialize<List<string>>(PhotoUrlsJson) ?? [];
        set => PhotoUrlsJson = JsonSerializer.Serialize(value);
    }
}