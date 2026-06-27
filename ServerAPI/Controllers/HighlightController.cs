using Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerAPI.Repositories.Highlights;

namespace ServerAPI.Controllers;

/// <summary>
/// API for highlights.
/// Controlleren er bevidst "tynd" og videresender logik til repository.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HighlightController : ControllerBase
{
    private readonly IHighlightRepository _repository;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<HighlightController> _logger;

    /// <summary>
    /// Repository + hosting environment injiceres via DI.
    /// _env bruges til at finde wwwroot path, når vi sletter billeder fra disk.
    /// </summary>
    public HighlightController(
        IHighlightRepository repository,
        IWebHostEnvironment env,
        ILogger<HighlightController> logger)
    {
        _repository = repository;
        _env = env;
        _logger = logger;
    }

    // =========================
    // READ
    // =========================

    /// <summary>
    /// Henter alle highlights.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Highlight>>> GetAll()
    {
        var highlights = await _repository.GetAll();
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = GetCurrentUserId();
            if (User.IsInRole("Admin"))
                return Ok(highlights);

            return Ok(highlights.Where(highlight => !highlight.IsPrivate || highlight.UserId == userId));
        }

        return Ok(highlights.Where(highlight => !highlight.IsPrivate));
    }

    /// <summary>
    /// Henter et highlight ud fra id.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{id}")]
    public async Task<ActionResult<Highlight>> GetById(int id)
    {
        var highlight = await _repository.GetById(id);
        if (highlight == null)
            return NotFound();

        if (highlight.IsPrivate &&
            (User.Identity?.IsAuthenticated != true ||
             (!User.IsInRole("Admin") && highlight.UserId != GetCurrentUserId())))
        {
            return NotFound();
        }

        return Ok(highlight);
    }

    /// <summary>
    /// Henter highlights pagineret (server-side).
    /// Bruges af UI til at vise et grid uden at hente alt.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<Highlight>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 6,
        [FromQuery] string? searchTerm = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var includePrivate = User.Identity?.IsAuthenticated == true;
        var result = await _repository.GetPaged(page, pageSize, searchTerm, fromDate, toDate, includePrivate);

        if (includePrivate && !User.IsInRole("Admin"))
        {
            var userId = GetCurrentUserId();
            var allowedItems = result.Items.Where(item => !item.IsPrivate || item.UserId == userId).ToList();
            result = new PagedResult<Highlight>(allowedItems, allowedItems.Count, page, pageSize);
        }

        return Ok(result);
    }

    // =========================
    // WRITE
    // =========================

    /// <summary>
    /// Opretter et nyt highlight.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Highlight>> Create(SaveHighlightRequest request)
    {
        if (HasInvalidHighlightText(request))
            return BadRequest(new { message = "Titel og beskrivelse skal udfyldes." });

        if (request.Date is null || request.Date.Value == default)
            return BadRequest(new { message = "Dato er ugyldig." });

        var highlight = ToHighlight(request);
        highlight.UserId = GetCurrentUserId();
        var created = await _repository.Add(highlight);
        _logger.LogInformation(
            "Highlight {HighlightId} was created by user {ActorUserId}. Private: {IsPrivate}.",
            created.Id,
            created.UserId,
            created.IsPrivate);

        // Returnerer 201 Created + lokation til GetById
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Opdaterer et eksisterende highlight.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] SaveHighlightRequest request)
    {
        if (HasInvalidHighlightText(request))
            return BadRequest(new { message = "Titel og beskrivelse skal udfyldes." });

        if (request.Date is null || request.Date.Value == default)
            return BadRequest(new { message = "Dato er ugyldig." });

        var existing = await _repository.GetById(id);
        if (existing is null)
            return NotFound();

        if (existing.IsPrivate && !User.IsInRole("Admin") && existing.UserId != GetCurrentUserId())
            return Forbid();

        var highlight = ToHighlight(request);
        highlight.Id = id;
        highlight.UserId = existing.UserId;
        await _repository.Update(highlight);
        _logger.LogInformation(
            "Highlight {HighlightId} was updated by user {ActorUserId}. Owner user: {OwnerUserId}. Private: {IsPrivate}.",
            highlight.Id,
            GetCurrentUserId(),
            highlight.UserId,
            highlight.IsPrivate);
        return NoContent();
    }

    /// <summary>
    /// Sletter et highlight.
    /// Hvis highlight har et billede (ImageUrl), forsøger vi også at slette filen fra wwwroot.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var highlight = await _repository.GetById(id);

        if (highlight is null)
            return NotFound();

        if (highlight.IsPrivate && !User.IsInRole("Admin") && highlight.UserId != GetCurrentUserId())
            return Forbid();

        var ownerUserId = highlight.UserId;

        // Hvis der ligger et billede på disk, prøver vi at fjerne det først.
        // (Failure her må ikke stoppe selve sletningen af highlight i DB.)
        if (!string.IsNullOrEmpty(highlight.ImageUrl))
        {
            try
            {
                // Fjern domæne fra URL, fx "http://localhost:5176/uploads/2025.07.14/abc.jpg"
                var relativePath = highlight.ImageUrl.Replace($"{Request.Scheme}://{Request.Host}", "");
                var fullPath = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/'));

                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);
            }
            catch (Exception ex)
            {
                // Bevidst "soft fail": selve highlight-sletningen må stadig gerne fortsætte.
                _logger.LogWarning(ex, "Could not delete image for highlight {HighlightId}.", id);
            }
        }

        await _repository.Delete(id);
        _logger.LogInformation(
            "Highlight {HighlightId} was deleted by user {ActorUserId}. Owner user: {OwnerUserId}.",
            id,
            GetCurrentUserId(),
            ownerUserId);
        return NoContent();
    }

    private int GetCurrentUserId()
    {
        var value = User.FindFirst("sub")?.Value;
        return int.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("Authenticated user is missing a valid user ID claim.");
    }

    private static bool HasInvalidHighlightText(SaveHighlightRequest request)
        => string.IsNullOrWhiteSpace(request.Title) ||
           string.IsNullOrWhiteSpace(request.Description);

    private static Highlight ToHighlight(SaveHighlightRequest request)
    {
        return new Highlight
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Date = request.Date!.Value,
            ImageUrl = request.ImageUrl?.Trim(),
            IsPrivate = request.IsPrivate
        };
    }
}
