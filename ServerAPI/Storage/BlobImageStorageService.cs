using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace ServerAPI.Storage;

public sealed class BlobImageStorageService : IImageStorageService
{
    private readonly BlobContainerClient _container;
    private readonly BlobStorageOptions _options;
    private readonly ILogger<BlobImageStorageService> _logger;
    private readonly SemaphoreSlim _containerInitializationLock = new(1, 1);
    private bool _containerInitialized;

    public BlobImageStorageService(
        IOptions<BlobStorageOptions> options,
        ILogger<BlobImageStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _container = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
    }

    public async Task<string> UploadImageAsync(IFormFile file, int? actorUserId, CancellationToken cancellationToken)
    {
        await EnsureContainerExistsAsync(cancellationToken);

        await using var input = file.OpenReadStream();
        using var image = await Image.LoadAsync(input, cancellationToken);

        if (image.Width > _options.MaxImageWidth)
        {
            image.Mutate(operation => operation.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(_options.MaxImageWidth, 0)
            }));
        }

        await using var output = new MemoryStream();
        await image.SaveAsWebpAsync(output, new WebpEncoder
        {
            Quality = _options.WebpQuality
        }, cancellationToken);
        output.Position = 0;

        var folderName = DateTime.UtcNow.ToString("yyyy.MM.dd");
        var blobName = $"uploads/{folderName}/{Guid.NewGuid():N}.webp";
        var blob = _container.GetBlobClient(blobName);

        await blob.UploadAsync(output, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "image/webp",
                CacheControl = "public, max-age=31536000, immutable"
            }
        }, cancellationToken);

        _logger.LogInformation(
            "Image uploaded to Blob Storage. Blob: {BlobName}. Original size: {OriginalSizeBytes} bytes. Stored size: {StoredSizeBytes} bytes. Actor user: {ActorUserId}.",
            blobName,
            file.Length,
            output.Length,
            actorUserId);

        return BuildPublicUrl(blobName, blob.Uri);
    }

    private async Task EnsureContainerExistsAsync(CancellationToken cancellationToken)
    {
        if (_containerInitialized)
            return;

        await _containerInitializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_containerInitialized)
                return;

            await _container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
            _containerInitialized = true;
        }
        finally
        {
            _containerInitializationLock.Release();
        }
    }

    public async Task<bool> TryDeleteImageAsync(string? imageUrl, CancellationToken cancellationToken)
    {
        var blobName = TryGetBlobName(imageUrl);
        if (string.IsNullOrWhiteSpace(blobName))
            return false;

        try
        {
            var response = await _container.GetBlobClient(blobName)
                .DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);

            if (response.Value)
                _logger.LogInformation("Deleted image from Blob Storage. Blob: {BlobName}.", blobName);

            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete image from Blob Storage. Blob: {BlobName}.", blobName);
            return false;
        }
    }

    public bool IsAllowedImageUrl(string? imageUrl)
        => string.IsNullOrWhiteSpace(imageUrl) || TryGetBlobName(imageUrl) is not null;

    private string BuildPublicUrl(string blobName, Uri blobUri)
    {
        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
            return blobUri.ToString();

        return $"{_options.PublicBaseUrl.TrimEnd('/')}/{blobName}";
    }

    private string? TryGetBlobName(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) ||
            !Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl) &&
            Uri.TryCreate(_options.PublicBaseUrl.TrimEnd('/'), UriKind.Absolute, out var publicBaseUri) &&
            string.Equals(uri.Host, publicBaseUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            var basePath = publicBaseUri.AbsolutePath.TrimEnd('/');
            var path = uri.AbsolutePath;

            if (string.IsNullOrEmpty(basePath) || path.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(path[(basePath.Length + 1)..]);
        }

        if (!string.Equals(uri.Host, _container.Uri.Host, StringComparison.OrdinalIgnoreCase))
            return null;

        var expectedPrefix = $"/{_options.ContainerName.Trim('/')}/";
        if (!uri.AbsolutePath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        return Uri.UnescapeDataString(uri.AbsolutePath[expectedPrefix.Length..]);
    }
}
