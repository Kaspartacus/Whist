using Core;
using MongoDB.Driver;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using ServerAPI.Configuration;
using ServerAPI.Utils;

namespace ServerAPI.Repositories.Highlights;

/// <summary>
/// MongoDB implementation af highlight repository.
/// Her ligger highlights i en separat collection: "highlights".
/// </summary>
public class HighlightRepositoryMongoDB : IHighlightRepository
{
    private readonly IMongoCollection<Highlight> _highlights;
    private readonly IMongoCollection<CounterDoc> _counters;
    private readonly ILogger<HighlightRepositoryMongoDB> _logger;

    /// <summary>
    /// Opretter forbindelse til MongoDB, initialiserer collections og forsøger at oprette et index på Date for hurtig sortering/paging.
    /// </summary>
    public HighlightRepositoryMongoDB(IConfiguration config, ILogger<HighlightRepositoryMongoDB> logger)
    {
        _logger = logger;
        var db = MongoDatabaseFactory.Create(config);
        _highlights = db.GetCollection<Highlight>("highlights");

        // Collection til tællere (counter pattern)
        _counters = db.GetCollection<CounterDoc>("counters");

        // Opret index på Date, så SortByDescending(Date) + paging bliver hurtigere ved mange highlights.
        // CreateOne er idempotent: Mongo genbruger eksisterende index med samme navn/definition.
        try
        {
            var dateIndex = new CreateIndexModel<Highlight>(
                Builders<Highlight>.IndexKeys.Descending(h => h.Date),
                new CreateIndexOptions { Name = "idx_highlights_date_desc" });

            _highlights.Indexes.CreateOne(dateIndex);
        }
        catch (Exception ex)
        {
            // Soft fail: hvis index allerede findes eller DB ikke tillader create i miljøet,
            // så skal appen stadig kunne køre.
            _logger.LogWarning(ex, "Could not create highlight date index. The app will continue without it.");
        }
    }

    // =========================
    // READ
    // =========================

    /// <summary>
    /// Returnerer alle highlights fra databasen (uden filtrering/paging).
    /// </summary>
    /// <inheritdoc />
    public async Task<IEnumerable<Highlight>> GetAll()
    {
        return await _highlights.Find(_ => true).ToListAsync();
    }

    /// <summary>
    /// Finder ét highlight ud fra Id (returnerer null hvis det ikke findes).
    /// </summary>
    /// <inheritdoc />
    public async Task<Highlight?> GetById(int id)
    {
        return await _highlights.Find(h => h.Id == id).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Henter highlights pagineret med valgfri søgning, dato-interval og mulighed for at inkludere/ekskludere private highlights.
    /// </summary>
    /// <inheritdoc />
    public async Task<PagedResult<Highlight>> GetPaged(
        int page,
        int pageSize,
        string? searchTerm = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        bool includePrivate = true)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 6;

        var skip = (page - 1) * pageSize;

        var filter = Builders<Highlight>.Filter.Empty;

        // Privat-filter: hvis includePrivate=false, så fjern private FØR paging
        if (!includePrivate)
            filter &= Builders<Highlight>.Filter.Eq(h => h.IsPrivate, false);

        // Søgning i Title/Description (case-insensitive regex)
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var regex = new BsonRegularExpression(searchTerm, "i");
            var titleFilter = Builders<Highlight>.Filter.Regex(h => h.Title, regex);
            var descFilter = Builders<Highlight>.Filter.Regex(h => h.Description, regex);
            filter &= Builders<Highlight>.Filter.Or(titleFilter, descFilter);
        }

        // Dato-interval (inklusive)
        if (fromDate.HasValue)
            filter &= Builders<Highlight>.Filter.Gte(h => h.Date, fromDate.Value.Date);

        if (toDate.HasValue)
            filter &= Builders<Highlight>.Filter.Lt(h => h.Date, toDate.Value.Date.AddDays(1));

        var totalCount = (int)await _highlights.CountDocumentsAsync(filter);

        var items = await _highlights.Find(filter)
            .SortByDescending(h => h.Date)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        return new PagedResult<Highlight>(items, totalCount, page, pageSize);
    }

    // =========================
    // WRITE
    // =========================

    /// <summary>
    /// Opretter et nyt highlight: sætter dato, genererer sekventielt Id og indsætter dokumentet i databasen.
    /// </summary>
    /// <inheritdoc />
    public async Task<Highlight> Add(Highlight highlight)
    {
        // NOTE: Adfærd bevares: dato sættes til "i dag" (DateTime.Today) ved oprettelse.
        highlight.Date = DateTime.Today;

        // NOTE: Id genereres sekventielt via counter-pattern (1, 2, 3, ...), uden at scanne hele collectionen.
        highlight.Id = await GetNextIdAsync();
        
        // Automatisk: erstatter "KSDH" med BIF<3.
        TextAutoReplace.Apply(highlight, _logger);

        await _highlights.InsertOneAsync(highlight);
        return highlight;
    }

    /// <summary>
    /// Sletter et highlight ud fra Id.
    /// </summary>
    /// <inheritdoc />
    public async Task Delete(int id)
    {
        var result = await _highlights.DeleteOneAsync(h => h.Id == id);
        if (result.DeletedCount == 0)
            _logger.LogWarning("Highlight {HighlightId} was not deleted because it was not found.", id);
    }

    /// <summary>
    /// Opdaterer et highlight ved at erstatte hele dokumentet med samme Id.
    /// </summary>
    /// <inheritdoc />
    public async Task Update(Highlight highlight)
    {
        // Automatisk: erstatter "KSDH" med BIF<3.
        TextAutoReplace.Apply(highlight, _logger);
        
        var result = await _highlights.ReplaceOneAsync(h => h.Id == highlight.Id, highlight);
        if (result.MatchedCount == 0)
            _logger.LogWarning("Highlight {HighlightId} was not updated because it was not found.", highlight.Id);
    }

    // =========================
    // Helpers
    // =========================

    /// <summary>
    /// Genererer næste sekventielle Id via counter-pattern (atomisk increment med upsert).
    /// </summary>
    private async Task<int> GetNextIdAsync()
    {
        // Automatisk: find counter doc for "highlights" og increment med 1.
        // Upsert gør at den oprettes automatisk første gang.
        var filter = Builders<CounterDoc>.Filter.Eq(c => c.Id, "highlights");
        var update = Builders<CounterDoc>.Update.Inc(c => c.Seq, 1);

        var options = new FindOneAndUpdateOptions<CounterDoc>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var counter = await _counters.FindOneAndUpdateAsync(filter, update, options);
        return counter.Seq;
    }

    /// <summary>
    /// Bruges til at generere sekventielle ids (max+1) uden at scanne hele collectionen.
    /// </summary>
    private class CounterDoc
    {
        // MongoDB convention: _id er en string key, fx "highlights"
        [BsonId]
        public string Id { get; set; } = default!;

        // Selve tælleren
        public int Seq { get; set; }
    }
}
