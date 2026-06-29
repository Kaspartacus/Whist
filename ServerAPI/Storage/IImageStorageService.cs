using Microsoft.AspNetCore.Http;

namespace ServerAPI.Storage;

public interface IImageStorageService
{
    Task<string> UploadImageAsync(IFormFile file, int? actorUserId, CancellationToken cancellationToken);
    Task<bool> TryDeleteImageAsync(string? imageUrl, CancellationToken cancellationToken);
}
