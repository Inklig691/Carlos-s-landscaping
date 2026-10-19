using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace BackEnd.Services;

public sealed class BlobPhotoStorageService(BlobServiceClient client) : IPhotoStorageService
{
    private static readonly HashSet<string> ImageTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxFileLength = 10 * 1024 * 1024;

    public async Task<string> UploadAsync(string container, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > MaxFileLength || !ImageTypes.Contains(file.ContentType))
            throw new InvalidDataException("Photos must be JPEG, PNG, or WebP images no larger than 10 MB.");

        var containerClient = client.GetBlobContainerClient(container);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var blob = containerClient.GetBlobClient($"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}");
        await using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType }
        }, cancellationToken);
        return blob.Uri.ToString();
    }

    public async Task DeleteAsync(string container, string url, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            await client.GetBlobContainerClient(container).GetBlobClient(Path.GetFileName(uri.AbsolutePath))
                .DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await client.GetBlobContainersAsync(cancellationToken: cancellationToken).AsPages(pageSizeHint: 1)
                .GetAsyncEnumerator(cancellationToken).MoveNextAsync();
            return true;
        }
        catch { return false; }
    }
}