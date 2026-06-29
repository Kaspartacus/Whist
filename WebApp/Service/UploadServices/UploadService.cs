using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using WebApp.Service.ApiErrors;

namespace WebApp.Service.UploadServices;

/// <summary>
/// HTTP-baseret upload-service.
/// 
/// Sender multipart/form-data til: POST api/upload/image
/// Backend gemmer billedet i Azure Blob Storage og returnerer JSON:
/// { "url": "https://..." }
/// </summary>
public class UploadService : IUploadService
{
    private const string UploadEndpoint = "api/upload/image";

    // Match serverens max.
    private const long MaxAllowedSizeBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private readonly HttpClient _http;

    public UploadService(HttpClient http)
    {
        _http = http;
    }

    public async Task<string?> UploadImageAsync(IBrowserFile file)
    {
        // Hurtig sanity-check
        if (file is null || file.Size == 0)
            return null;

        if (file.Size > MaxAllowedSizeBytes)
            throw new InvalidOperationException("Filen er for stor. Maks 5 MB tilladt.");

        var extension = Path.GetExtension(file.Name);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("Filtypen er ikke tilladt. Kun jpg, jpeg, png og webp er tilladt.");

        if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
            throw new InvalidOperationException("Filtypen er ikke tilladt. Kun jpg, jpeg, png og webp er tilladt.");
        
        // Multipart content skal disponeres korrekt
        await using var stream = file.OpenReadStream(maxAllowedSize: MaxAllowedSizeBytes);

        using var content = new MultipartFormDataContent();

        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

        // "file" skal matche backend [FromForm] IFormFile file
        content.Add(fileContent, "file", file.Name);

        var response = await _http.PostAsync(UploadEndpoint, content);

        var result = await response.ReadFromJsonOrThrowAsync<UploadResult>();
        return string.IsNullOrWhiteSpace(result?.Url) ? null : result.Url;
    }

    /// <summary>
    /// DTO til at læse serverens response.
    /// </summary>
    private sealed class UploadResult
    {
        public string Url { get; set; } = "";
    }
}
