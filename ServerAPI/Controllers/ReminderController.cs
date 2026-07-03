using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerAPI.Repositories.Calendars;
using ServerAPI.Services.Reminders;

namespace ServerAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ReminderController : ControllerBase
{
    private const string SecretHeaderName = "X-Reminder-Secret";
    private static readonly TimeZoneInfo DanishTimeZone = FindDanishTimeZone();

    private readonly IReminderMailService _reminderMailService;
    private readonly ICalendarRepository _calendarRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReminderController> _logger;

    public ReminderController(
        IReminderMailService reminderMailService,
        ICalendarRepository calendarRepository,
        IConfiguration configuration,
        ILogger<ReminderController> logger)
    {
        _reminderMailService = reminderMailService;
        _calendarRepository = calendarRepository;
        _configuration = configuration;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("run")]
    [EnableRateLimiting("reminders")]
    public async Task<IActionResult> Run(CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
        {
            _logger.LogWarning(
                "Unauthorized reminder run attempt. IP: {IpAddress}.",
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "Unauthorized." });
        }

        var result = await _reminderMailService.RunOnce(cancellationToken);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("event-today")]
    [EnableRateLimiting("reminders")]
    public async Task<IActionResult> HasEventToday()
    {
        if (!IsAuthorized())
        {
            _logger.LogWarning(
                "Unauthorized event-today check attempt. IP: {IpAddress}.",
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "Unauthorized." });
        }

        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, DanishTimeZone);
        var today = DateOnly.FromDateTime(now.DateTime);
        var todayDate = today.ToDateTime(TimeOnly.MinValue).Date;
        var eventsToday = (await _calendarRepository.GetAll())
            .Where(calendarEvent => calendarEvent.Date.Date == todayDate)
            .ToList();

        if (eventsToday.Count == 0)
            return NoContent();

        return Ok(new
        {
            hasEventToday = true,
            date = today.ToString("yyyy-MM-dd"),
            eventCount = eventsToday.Count
        });
    }

    private bool IsAuthorized()
    {
        var expectedSecret = _configuration["Reminders:RunSecret"];
        if (string.IsNullOrWhiteSpace(expectedSecret))
            return false;

        if (!Request.Headers.TryGetValue(SecretHeaderName, out var providedValues))
            return false;

        var providedSecret = providedValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedSecret))
            return false;

        var expectedBytes = Encoding.UTF8.GetBytes(expectedSecret);
        var providedBytes = Encoding.UTF8.GetBytes(providedSecret);
        return expectedBytes.Length == providedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static TimeZoneInfo FindDanishTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
        }
    }
}
