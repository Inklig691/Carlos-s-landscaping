namespace BackEnd.Services;

public interface IPhotoStorageService
{
    Task<string> UploadAsync(string container, IFormFile file, CancellationToken cancellationToken);
    Task DeleteAsync(string container, string url, CancellationToken cancellationToken);
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken);
}