using Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerAPI.Repositories.Calendars;

namespace ServerAPI.Controllers;

/// <summary>
/// API-controller for kalender-events.
/// Controlleren er bevidst "tynd":
/// - Ingen mail-logik her (det ligger i MailReminderWorker)
/// - Ingen database-specifik logik her (det ligger i repository)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CalendarController : ControllerBase
{
    private readonly ICalendarRepository _repo;
    private readonly ILogger<CalendarController> _logger;

    public CalendarController(ICalendarRepository repo, ILogger<CalendarController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <summary>Henter alle kalender-events.</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<Calendar>>> GetAll()
    {
        var items = await _repo.GetAll();
        return Ok(items);
    }

    /// <summary>
    /// Gemmer et event (opret/ret).
    /// Repository håndterer selv om det er add/update.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Save(SaveCalendarRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Note))
            return BadRequest(new { message = "Note er påkrævet." });

        if (request.Date == default)
            return BadRequest(new { message = "Dato er ugyldig." });

        var calendar = new Calendar
        {
            Date = request.Date,
            Note = request.Note.Trim()
        };

        await _repo.AddOrUpdate(calendar);
        _logger.LogInformation(
            "Calendar event {CalendarId} was saved by user {ActorUserId}. Date: {EventDate:u}.",
            calendar.Id,
            GetCurrentUserId(),
            calendar.Date);
        return Ok();
    }

    /// <summary>Sletter et event ud fra id.</summary>
    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        await _repo.Delete(id);
        _logger.LogInformation(
            "Calendar event {CalendarId} was deleted by user {ActorUserId}.",
            id,
            GetCurrentUserId());
        return Ok();
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirst("sub")?.Value;
        return int.TryParse(value, out var userId) ? userId : null;
    }
}
