using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ServerAPI.Controllers;

/// <summary>
/// Upload controller til profilbilleder.
/// Gemmer filer i: wwwroot/uploads/{yyyy.MM.dd}/{guid}.{ext}
/// Returnerer en offentlig URL til filen.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<UploadController> _logger;

    // Tilladte filtyper til upload
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    // Maks filstørrelse (5 MB)
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    public UploadController(IWebHostEnvironment env, ILogger<UploadController> logger)
    {
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Upload af billede via multipart/form-data.
    /// Feltet skal hedde "file".
    /// </summary>
    [HttpPost("image")]
    [Authorize]
    [EnableRateLimiting("upload")]
    public async Task<IActionResult> UploadImage([FromForm] IFormFile file, CancellationToken ct)
    {
        // 1) Basis validering
        if (file is null || file.Length == 0)
        {
            _logger.LogWarning("Image upload rejected for user {ActorUserId}: no file was received.", GetCurrentUserId());
            return BadRequest(new { message = "Intet billede modtaget." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            _logger.LogWarning(
                "Image upload rejected for user {ActorUserId}: file was too large. Size: {FileSizeBytes} bytes.",
                GetCurrentUserId(),
                file.Length);
            return BadRequest(new { message = "Filen er for stor. Maks 5 MB tilladt." });
        }

        // 2) Valider filtype ud fra extension
        // NOTE: Dette er ikke "virus scanning" eller fuld MIME-validering, men fint til jeres use-case.
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

        // 3) Byg upload sti (wwwroot/uploads/yyyy.MM.dd/)
        var fileName = $"{Guid.NewGuid()}{ext.ToLowerInvariant()}";
        var folderName = DateTime.UtcNow.ToString("yyyy.MM.dd");
        var uploadPath = Path.Combine(_env.WebRootPath, "uploads", folderName);

        // Opret mappe hvis den ikke findes
        Directory.CreateDirectory(uploadPath);

        // 4) Gem filen
        var filePath = Path.Combine(uploadPath, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream, ct);
        }

        // 5) Returnér offentligt URL (kræver at static files er enabled i backend)
        var fileUrl = $"{Request.Scheme}://{Request.Host}/uploads/{folderName}/{fileName}";
        _logger.LogInformation(
            "Image uploaded by user {ActorUserId}. Stored as {StoredFileName}. Size: {FileSizeBytes} bytes.",
            GetCurrentUserId(),
            fileName,
            file.Length);
        return Ok(new { Url = fileUrl });
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirst("sub")?.Value;
        return int.TryParse(value, out var userId) ? userId : null;
    }
}
