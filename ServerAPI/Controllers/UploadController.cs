using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ServerAPI.Storage;
using SixLabors.ImageSharp;

namespace ServerAPI.Controllers;

/// <summary>
/// Upload controller til profilbilleder.
/// Validerer billeder og gemmer kun en komprimeret version i Azure Blob Storage.
/// Returnerer en offentlig URL til filen.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly IImageStorageService _imageStorage;
    private readonly BlobStorageOptions _blobOptions;
    private readonly ILogger<UploadController> _logger;

    // Tilladte filtyper til upload
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    public UploadController(
        IImageStorageService imageStorage,
        IOptions<BlobStorageOptions> blobOptions,
        ILogger<UploadController> logger)
    {
        _imageStorage = imageStorage;
        _blobOptions = blobOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Upload af billede via multipart/form-data.
    /// Feltet skal hedde "file".
    /// </summary>
    [HttpPost("image")]
    [Authorize]
    [EnableRateLimiting("upload")]
    public async Task<IActionResult> UploadImage([FromForm] UploadImageRequest request, CancellationToken ct)
    {
        var file = request.File;

        // 1) Basis validering
        if (file is null || file.Length == 0)
        {
            _logger.LogWarning("Image upload rejected for user {ActorUserId}: no file was received.", GetCurrentUserId());
            return BadRequest(new { message = "Intet billede modtaget." });
        }

        if (file.Length > _blobOptions.MaxUploadBytes)
        {
            _logger.LogWarning(
                "Image upload rejected for user {ActorUserId}: file was too large. Size: {FileSizeBytes} bytes.",
                GetCurrentUserId(),
                file.Length);
            return BadRequest(new { message = "Filen er for stor. Maks 5 MB tilladt." });
        }

        var originalName = Path.GetFileName(file.FileName); // undgå evt. path traversal
        var ext = Path.GetExtension(originalName);

        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            _logger.LogWarning(
                "Image upload rejected for user {ActorUserId}: file extension {Extension} is not allowed.",
                GetCurrentUserId(),
                ext);
            return BadRequest(new { message = "Filtypen er ikke tilladt. Kun jpg, jpeg, png og webp er tilladt." });
        }

        if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
        {
            _logger.LogWarning(
                "Image upload rejected for user {ActorUserId}: content type {ContentType} is not allowed.",
                GetCurrentUserId(),
                file.ContentType);
            return BadRequest(new { message = "Filtypen er ikke tilladt. Kun jpg, jpeg, png og webp er tilladt." });
        }

        string fileUrl;
        try
        {
            fileUrl = await _imageStorage.UploadImageAsync(file, GetCurrentUserId(), ct);
        }
        catch (UnknownImageFormatException ex)
        {
            _logger.LogWarning(
                ex,
                "Image upload rejected for user {ActorUserId}: file could not be decoded as an image.",
                GetCurrentUserId());
            return BadRequest(new { message = "Billedet kunne ikke læses. Vælg en gyldig jpg, png eller webp fil." });
        }

        _logger.LogInformation(
            "Image uploaded by user {ActorUserId}. Original file: {OriginalFileName}. Size: {FileSizeBytes} bytes.",
            GetCurrentUserId(),
            originalName,
            file.Length);
        return Ok(new { Url = fileUrl });
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirst("sub")?.Value;
        return int.TryParse(value, out var userId) ? userId : null;
    }

    public sealed class UploadImageRequest
    {
        public IFormFile? File { get; set; }
    }
}
