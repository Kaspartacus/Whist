using Core;
using ServerAPI.Configuration;
using ServerAPI.Utils;

namespace ServerAPI.Repositories.Calendars;

/// <summary>
/// Cosmos DB repository for kalender.
/// </summary>
public class CalendarRepositoryCosmosDb : ICalendarRepository
{
    private readonly CosmosDbContext _cosmos;
    private readonly ILogger<CalendarRepositoryCosmosDb> _logger;

    public CalendarRepositoryCosmosDb(CosmosDbContext cosmos, ILogger<CalendarRepositoryCosmosDb> logger)
    {
        _cosmos = cosmos;
        _logger = logger;
    }

    public async Task<List<Calendar>> GetAll()
    {
        return await CosmosDbContext.ReadAllAsync<Calendar>(_cosmos.Calendar);
    }

    public async Task<Calendar?> GetById(int id)
    {
        return await CosmosDbContext.ReadByDocumentIdAsync<Calendar>(_cosmos.Calendar, id.ToString());
    }

    public async Task<Calendar?> GetByDate(DateTime date)
    {
        var utcDateOnly = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var calendarEvents = await GetAll();
        return calendarEvents.FirstOrDefault(x => x.Date == utcDateOnly);
    }

    public async Task<bool> AddOrUpdate(Calendar calendarEvent)
    {
        calendarEvent.Date = DateTime.SpecifyKind(calendarEvent.Date.Date, DateTimeKind.Utc);

        var existing = calendarEvent.Id > 0
            ? await GetById(calendarEvent.Id)
            : await GetByDate(calendarEvent.Date);

        if (existing is null && calendarEvent.Id > 0)
        {
            _logger.LogWarning("Calendar event {CalendarId} was not updated because it was not found.", calendarEvent.Id);
            return false;
        }

        if (existing is null)
        {
            calendarEvent.Id = await _cosmos.GetNextIdAsync("calendar");
            calendarEvent.ReminderSent = false;
        }
        else
        {
            calendarEvent.Id = existing.Id;
            calendarEvent.ReminderSent = existing.Date.Date == calendarEvent.Date.Date && existing.ReminderSent;
        }

        TextAutoReplace.Apply(calendarEvent, _logger);

        await CosmosDbContext.UpsertAsync(_cosmos.Calendar, calendarEvent.Id.ToString(), calendarEvent);
        return true;
    }

    public async Task<bool> Delete(int id)
    {
        var existing = await CosmosDbContext.ReadByDocumentIdAsync<Calendar>(_cosmos.Calendar, id.ToString());
        if (existing is null)
        {
            _logger.LogWarning("Calendar event {CalendarId} was not deleted because it was not found.", id);
            return false;
        }

        await CosmosDbContext.DeleteAsync(_cosmos.Calendar, id.ToString());
        return true;
    }

    public async Task<List<Calendar>> FindPendingReminders(int reminderDaysAhead)
    {
        var tz = GetCopenhagenTimeZone();
        var todayLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz).Date;
        var latestReminderDate = DateTime.SpecifyKind(todayLocal.AddDays(reminderDaysAhead), DateTimeKind.Utc);
        var todayUtcDate = DateTime.SpecifyKind(todayLocal, DateTimeKind.Utc);

        var calendarEvents = await GetAll();
        return calendarEvents
            .Where(x => x.Date >= todayUtcDate && x.Date <= latestReminderDate && !x.ReminderSent)
            .ToList();
    }

    public async Task MarkReminderSent(int calendarId)
    {
        var calendarEvent = await CosmosDbContext.ReadByDocumentIdAsync<Calendar>(_cosmos.Calendar, calendarId.ToString());
        if (calendarEvent is null)
        {
            _logger.LogWarning("Calendar event {CalendarId} was not marked as reminder sent because it was not found.", calendarId);
            return;
        }

        calendarEvent.ReminderSent = true;
        await CosmosDbContext.UpsertAsync(_cosmos.Calendar, calendarEvent.Id.ToString(), calendarEvent);
    }

    public async Task MarkRemindersSent(IEnumerable<int> calendarIds)
    {
        var ids = calendarIds?.Distinct().ToArray() ?? Array.Empty<int>();
        if (ids.Length == 0) return;

        var matchedCount = 0;
        foreach (var id in ids)
        {
            var calendarEvent = await CosmosDbContext.ReadByDocumentIdAsync<Calendar>(_cosmos.Calendar, id.ToString());
            if (calendarEvent is null) continue;

            calendarEvent.ReminderSent = true;
            await CosmosDbContext.UpsertAsync(_cosmos.Calendar, calendarEvent.Id.ToString(), calendarEvent);
            matchedCount++;
        }

        if (matchedCount < ids.Length)
        {
            _logger.LogWarning(
                "Only {MatchedCount} of {RequestedCount} calendar events were found when marking reminders as sent.",
                matchedCount,
                ids.Length);
        }
    }

    private static TimeZoneInfo GetCopenhagenTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");
        }
    }
}
