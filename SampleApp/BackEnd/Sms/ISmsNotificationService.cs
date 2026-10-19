namespace BackEnd.Sms;

public interface ISmsNotificationService
{
    Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken);
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken);
}