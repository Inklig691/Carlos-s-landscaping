using Azure.Communication.Sms;

namespace BackEnd.Sms;

public sealed class LocalSmsNotificationService(ILogger<LocalSmsNotificationService> logger) : ISmsNotificationService
{
    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Local SMS to {PhoneNumber}: {Message}", phoneNumber, message);
        return Task.CompletedTask;
    }

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken) => Task.FromResult(true);
}

public sealed class AzureSmsNotificationService(SmsClient client, string senderNumber) : ISmsNotificationService
{
    public async Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken)
        => await client.SendAsync(senderNumber, phoneNumber, message, cancellationToken: cancellationToken);

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken) => Task.FromResult(true);
}