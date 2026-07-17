using Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerAPI.Repositories.Points;

namespace ServerAPI.Controllers;

/// <summary>
/// API-controller for Whist points.
/// Ansvar:
/// - Eksponere simple endpoints til UI'et (WhistSchemePage)
/// - Holde controller "tynd": ingen forretningslogik her
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PointController : ControllerBase
{
    private readonly IPointRepository _repository;
    private readonly ILogger<PointController> _logger;

    public PointController(IPointRepository repository, ILogger<PointController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    // =========================
    // READ
    // =========================

    /// <summary>
    /// Henter alle point entries.
    /// UI bruger dette til at beregne totals pr. spiller.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<PointEntry>>> GetAll()
    {
        var points = await _repository.GetAll();
        return Ok(points);
    }

    // =========================
    // WRITE
    // =========================

    /// <summary>
    /// Tilføjer en ny point entry.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult> Add(CreatePointRequest request)
    {
        if (request.PlayerId <= 0)
            return BadRequest(new { message = "Spiller er påkrævet." });

        if (request.Points == 0)
            return BadRequest(new { message = "Point må ikke være 0." });

        if (request.Date == default)
            return BadRequest(new { message = "Dato er ugyldig." });

        var point = new PointEntry
        {
            PlayerId = request.PlayerId,
            Points = request.Points,
            Date = request.Date
        };

        var created = await _repository.Add(point);
        if (!created)
            return BadRequest(new { message = "Spilleren findes ikke." });

        _logger.LogInformation(
            "Point entry {PointEntryId} was created for player {PlayerId} by user {ActorUserId}. Points: {Points}.",
            point.Id,
            point.PlayerId,
            GetCurrentUserId(),
            point.Points);
        return Ok();
    }

    /// <summary>
    /// Sletter en enkelt point entry ud fra id.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<ActionResult> Delete(int id)
    {
        var deleted = await _repository.Delete(id);
        if (!deleted)
            return NotFound(new { message = "Point-rækken blev ikke fundet." });

        _logger.LogInformation(
            "Point entry {PointEntryId} was deleted by user {ActorUserId}.",
            id,
            GetCurrentUserId());
        return Ok();
    }

    /// <summary>
    /// Sletter alle point entries (nulstil).
    /// Bruges når points konverteres til bøder.
    /// </summary>
    [HttpDelete("all")]
    [Authorize]
    public async Task<ActionResult> DeleteAll()
    {
        await _repository.DeleteAll();
        _logger.LogWarning(
            "All point entries were deleted by user {ActorUserId}.",
            GetCurrentUserId());
        return Ok();
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirst("sub")?.Value;
        return int.TryParse(value, out var userId) ? userId : null;
    }
}
