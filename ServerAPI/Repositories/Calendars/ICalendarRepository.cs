using Core;

namespace ServerAPI.Repositories.Calendars;

/// <summary>
/// Repository-kontrakt for kalender.
/// </summary>
public interface ICalendarRepository
{
    /// <summary>Henter alle kalender-events.</summary>
    Task<List<Calendar>> GetAll();

    // Henter et event ud fra dato.
    Task<Calendar?> GetByDate(DateTime date);

    /// <summary>
    /// Opretter/retter event for en dato.
    /// Dato håndteres som "date-only".
    /// </summary>
    Task<bool> AddOrUpdate(Calendar evt);

    /// <summary>Henter et event ud fra id.</summary>
    Task<Calendar?> GetById(int id);

    /// <summary>Sletter et event ud fra id.</summary>
    Task<bool> Delete(int id);

    /// <summary>
    /// Finder kommende events inden for reminder-vinduet (i lokal timezone),
    /// som ikke allerede har fået sendt reminder.
    /// </summary>
    Task<List<Calendar>> FindPendingReminders(int reminderDaysAhead);

    /// <summary>Markerer ét event som sendt.</summary>
    Task MarkReminderSent(int calendarId);

    /// <summary>
    /// Marker flere events som sendt i ét kald (bedre performance).
    /// </summary>
    Task MarkRemindersSent(IEnumerable<int> calendarIds);
}
