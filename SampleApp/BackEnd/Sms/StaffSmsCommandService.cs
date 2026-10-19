using System.Globalization;
using System.Text.RegularExpressions;
using BackEnd.Data;
using BackEnd.Models;
using Microsoft.EntityFrameworkCore;

namespace BackEnd.Sms;

public sealed partial class StaffSmsCommandService(
    AppDbContext db,
    ISmsNotificationService sms,
    ILogger<StaffSmsCommandService> logger)
{
    [GeneratedRegex(@"^\$(?<price>\d+(?:\.\d{1,2})?)\s+(?<reference>[A-Z0-9]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PriceCommand();

    public async Task<bool> ProcessAsync(string sender, string configuredStaffNumber, string message, CancellationToken token)
    {
        if (!NormalizePhone(sender).Equals(NormalizePhone(configuredStaffNumber), StringComparison.Ordinal))
            return false;

        var price = PriceCommand().Match(message.Trim());
        var parts = message.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = price.Success ? "PRICE" : parts.FirstOrDefault()?.ToUpperInvariant();
        var reference = price.Success ? price.Groups["reference"].Value : parts.ElementAtOrDefault(1);
        if (reference is null) return false;

        var appointment = await db.AppointmentRequests.SingleOrDefaultAsync(
            item => item.Reference == reference.ToUpperInvariant(), token);
        if (appointment is null) return false;

        var language = appointment.PreferredLanguage;
        switch (command)
        {
            case "PRICE" when price.Success && decimal.TryParse(price.Groups["price"].Value,
                NumberStyles.Number, CultureInfo.InvariantCulture, out var amount):
                appointment.QuotedPrice = amount;
                await db.SaveChangesAsync(token);
                if (appointment.SmsConsent)
                    await sms.SendAsync(appointment.Phone, LocalizedPrice(appointment, amount), token);
                return true;
            case "OK" when parts.Length == 2:
                appointment.Status = "Approved";
                await db.SaveChangesAsync(token);
                if (appointment.SmsConsent)
                    await sms.SendAsync(appointment.Phone, language == "es-US"
                        ? "Su cita ha sido aprobada." : "Your appointment has been approved.", token);
                return true;
            case "UNAVAILABLE" when parts.Length == 2:
                appointment.Status = "PendingReschedule";
                await db.SaveChangesAsync(token);
                if (appointment.SmsConsent)
                    await sms.SendAsync(appointment.Phone, language == "es-US"
                        ? "No podemos atenderle en el día o la hora solicitados. Por favor, reprograme su cita."
                        : "We cannot serve you at the requested date or time. Please reschedule your appointment.", token);
                return true;
            case "DAY" when parts.Length == 3 && DateTime.TryParseExact(
                parts[2], "M/d", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDay):
                if (appointment.SmsConsent)
                {
                    var day = DateOnly.FromDateTime(parsedDay);
                    await sms.SendAsync(appointment.Phone, language == "es-US"
                        ? $"El día más próximo disponible es {day:M/d}; por favor, reprograme su cita."
                        : $"Earliest available day is {day:M/d}; please reschedule.", token);
                }
                return true;
            case "TIME" when parts.Length == 3 && TimeOnly.TryParse(parts[2], CultureInfo.InvariantCulture, out var time)
                && time.Hour >= 16 && time.Hour < 20:
                if (appointment.SmsConsent)
                    await sms.SendAsync(appointment.Phone, language == "es-US"
                        ? $"Hay disponibilidad de {time:h:mm tt} a 8:00 p. m.; por favor, reprograme su cita."
                        : $"Available from {time:h:mm tt} through 8:00 pm; please reschedule.", token);
                return true;
            default:
                logger.LogWarning("Ignored invalid staff SMS command for reference {Reference}", reference);
                return false;
        }
    }

    private static string LocalizedPrice(AppointmentRequest appointment, decimal amount)
    {
        var service = appointment.ServiceId switch
        {
            "sprinkler-repair" => appointment.PreferredLanguage == "es-US" ? "reparación del sistema de riego" : "sprinkler system repair",
            "palm-trees" => appointment.PreferredLanguage == "es-US" ? "servicio de palmeras" : "palm tree service",
            "planting" => appointment.PreferredLanguage == "es-US" ? "plantación" : "planting",
            _ => appointment.PreferredLanguage == "es-US" ? "mantenimiento exterior" : "outside maintenance"
        };
        return appointment.PreferredLanguage == "es-US"
            ? $"El precio de su cita para {service} es ${amount:0.00}."
            : $"The price of your appointment for {service} is ${amount:0.00}.";
    }

    private static string NormalizePhone(string value) => new(value.Where(char.IsDigit).ToArray());
}