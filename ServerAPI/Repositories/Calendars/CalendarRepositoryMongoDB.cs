using Core;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using ServerAPI.Configuration;
using ServerAPI.Utils;

namespace ServerAPI.Repositories.Calendars;

/// <summary>
/// MongoDB repository for kalender.
/// Fokus: robusthed + lav vedligeholdelse.
/// </summary>
public class CalendarRepositoryMongoDB : ICalendarRepository
{
    private const string CalendarCollectionName = "calendar";
    private const string CounterCollectionName = "counters";
    private const string CalendarCounterKey = "calendar";

    private readonly IMongoCollection<Calendar> _calendar;
    private readonly IMongoCollection<CounterDoc> _counters;
    private readonly ILogger<CalendarRepositoryMongoDB> _logger;

    /// <summary>
    /// Initialiserer repository: opretter MongoDB-klient, vælger DB/collections og sikrer relevante indexes.
    /// </summary>
    public CalendarRepositoryMongoDB(IConfiguration config, ILogger<CalendarRepositoryMongoDB> logger)
    {
        _logger = logger;
        var db = MongoDatabaseFactory.Create(config);

        _calendar = db.GetCollection<Calendar>(CalendarCollectionName);
        _counters = db.GetCollection<CounterDoc>(CounterCollectionName);

        EnsureIndexes();
    }

    /// <summary>
    /// Sikrer nødvendige indexes for kalender-collection (unik dato + worker-venlige søgefelter).
    /// </summary>
    private void EnsureIndexes()
    {
        // 1) Én event pr. dato (unik)
        var dateIndex = new CreateIndexModel<Calendar>(
            Builders<Calendar>.IndexKeys.Ascending(x => x.Date),
            new CreateIndexOptions { Unique = true });

        // 2) Worker-kald: ReminderSent + Date
        var reminderIndex = new CreateIndexModel<Calendar>(
            Builders<Calendar>.IndexKeys.Ascending(x => x.ReminderSent).Ascending(x => x.Date));

        _calendar.Indexes.CreateMany(new[] { dateIndex, reminderIndex });
    }

    /// <summary>
    /// Returnerer alle kalender-events fra databasen.
    /// </summary>
    public async Task<List<Calendar>> GetAll()
    {
        return await _calendar.Find(_ => true).ToListAsync();
    }

    /// <summary>
    /// Finder et kalender-event ud fra en dato (dato behandles som UTC "date-only").
    /// </summary>
    public async Task<Calendar?> GetByDate(DateTime date)
    {
        // Dato gemmes som UTC "date-only"
        var utcDateOnly = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        return await _calendar.Find(x => x.Date == utcDateOnly).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Opretter et nyt event eller opdaterer eksisterende event på samme dato (upsert), uden at nulstille ReminderSent.
    /// </summary>
    public async Task AddOrUpdate(Calendar calendarEvent)
    {
        // Ensure date stored as UTC date-only
        calendarEvent.Date = DateTime.SpecifyKind(calendarEvent.Date.Date, DateTimeKind.Utc);

        // Hvis event allerede findes på datoen, så skal vi bevare eksisterende Id og ReminderSent
        // (så vi ikke "nulstiller" ReminderSent ved en edit).
        var existing = await GetByDate(calendarEvent.Date);

        if (existing is null)
        {
            // Ny dato → nyt id (O(1) counter)
            calendarEvent.Id = await GetNextId();

            // ReminderSent skal normalt være false for et nyt event.
            // (Modelens default er false, men vi sætter den eksplicit for tydelighed.)
            calendarEvent.ReminderSent = false;
        }
        else
        {
            // Eksisterende dato → bevar id + reminder state
            calendarEvent.Id = existing.Id;
            calendarEvent.ReminderSent = existing.ReminderSent;
        }
        
        // Automatisk: erstatter "KSDH" med BIF<3.
        TextAutoReplace.Apply(calendarEvent, _logger);

        // Upsert på dato (unik index sørger for at der ikke kan komme dubletter).
        var filter = Builders<Calendar>.Filter.Eq(x => x.Date, calendarEvent.Date);
        await _calendar.ReplaceOneAsync(filter, calendarEvent, new ReplaceOptions { IsUpsert = true });
    }

    /// <summary>
    /// Sletter et kalender-event ud fra dets Id.
    /// </summary>
    public async Task Delete(int id)
    {
        var result = await _calendar.DeleteOneAsync(c => c.Id == id);
        if (result.DeletedCount == 0)
            _logger.LogWarning("Calendar event {CalendarId} was not deleted because it was not found.", id);
    }

    /// <summary>
    /// Finder events der ligger præcis offsetDays fra i dag (lokal tid), og som endnu ikke har fået sendt reminder.
    /// </summary>
    public async Task<List<Calendar>> FindByExactOffsetDays(int offsetDays)
    {
        // Europe/Copenhagen (Windows-id = "Central Europe Standard Time")
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");
        var todayLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz).Date;
        var targetLocalDate = todayLocal.AddDays(offsetDays);

        // Vi gemmer datoer som "hele dage" (utc-dato uden tid)
        var targetUtcDate = DateTime.SpecifyKind(targetLocalDate, DateTimeKind.Utc);

        var filter = Builders<Calendar>.Filter.And(
            Builders<Calendar>.Filter.Eq(x => x.Date, targetUtcDate),
            Builders<Calendar>.Filter.Eq(x => x.ReminderSent, false)
        );

        return await _calendar.Find(filter).ToListAsync();
    }

    /// <summary>
    /// Marker et enkelt kalender-event som "reminder sendt" (ReminderSent = true).
    /// </summary>
    public async Task MarkReminderSent(int calendarId)
    {
        var update = Builders<Calendar>.Update.Set(x => x.ReminderSent, true);
        var result = await _calendar.UpdateOneAsync(c => c.Id == calendarId, update);
        if (result.MatchedCount == 0)
            _logger.LogWarning("Calendar event {CalendarId} was not marked as reminder sent because it was not found.", calendarId);
    }

    /// <summary>
    /// Marker flere kalender-events som "reminders sendt" i ét bulk update-kald.
    /// </summary>
    public async Task MarkRemindersSent(IEnumerable<int> calendarIds)
    {
        var ids = calendarIds?.Distinct().ToArray() ?? Array.Empty<int>();
        if (ids.Length == 0) return;

        var filter = Builders<Calendar>.Filter.In(x => x.Id, ids);
        var update = Builders<Calendar>.Update.Set(x => x.ReminderSent, true);

        var result = await _calendar.UpdateManyAsync(filter, update);
        if (result.MatchedCount < ids.Length)
        {
            _logger.LogWarning(
                "Only {MatchedCount} of {RequestedCount} calendar events were found when marking reminders as sent.",
                result.MatchedCount,
                ids.Length);
        }
    }

    // --------------------------
    // Counter (O(1) id)
    // --------------------------

    /// <summary>
    /// Henter næste sekventielle Id via counter-pattern (FindOneAndUpdate + Inc), så vi undgår at scanne hele collectionen.
    /// </summary>
    private async Task<int> GetNextId()
    {
        var filter = Builders<CounterDoc>.Filter.Eq(x => x.Id, CalendarCounterKey);
        var update = Builders<CounterDoc>.Update.Inc(x => x.Seq, 1);

        var options = new FindOneAndUpdateOptions<CounterDoc>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var doc = await _counters.FindOneAndUpdateAsync(filter, update, options);
        return doc.Seq;
    }

    private class CounterDoc
    {
        [BsonId]
        public string Id { get; set; } = default!;

        public int Seq { get; set; }
    }
}
