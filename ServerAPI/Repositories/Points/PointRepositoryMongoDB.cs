using Core;
using MongoDB.Driver;
using ServerAPI.Configuration;

namespace ServerAPI.Repositories.Points;

/// <summary>
/// MongoDB implementation af points repository.
/// Points ligger i en separat collection: "points".
/// </summary>
public class PointRepositoryMongoDB : IPointRepository
{
    private const string CollectionName = "points";

    private readonly IMongoCollection<PointEntry> _points;
    private readonly ILogger<PointRepositoryMongoDB> _logger;

    /// <summary>
    /// Opretter forbindelse til MongoDB baseret på appsettings og initialiserer points-collection.
    /// </summary>
    public PointRepositoryMongoDB(IConfiguration config, ILogger<PointRepositoryMongoDB> logger)
    {
        _logger = logger;
        var db = MongoDatabaseFactory.Create(config);
        _points = db.GetCollection<PointEntry>(CollectionName);
    }

    // =========================
    // READ
    // =========================

    /// <summary>
    /// Returnerer alle point-entries fra databasen.
    /// </summary>
    /// <inheritdoc />
    public async Task<List<PointEntry>> GetAll()
    {
        return await _points.Find(_ => true).ToListAsync();
    }

    // =========================
    // WRITE
    // =========================

    /// <summary>
    /// Opretter et nyt point-entry ved at generere et Id og indsætte dokumentet i databasen.
    /// </summary>
    /// <inheritdoc />
    public async Task Add(PointEntry point)
    {
        // Bevar adfærd: Id genereres sekventielt.
        point.Id = await GetNextId();
        await _points.InsertOneAsync(point);
    }

    /// <summary>
    /// Sletter et point-entry ud fra Id.
    /// </summary>
    /// <inheritdoc />
    public async Task Delete(int id)
    {
        var result = await _points.DeleteOneAsync(p => p.Id == id);
        if (result.DeletedCount == 0)
            _logger.LogWarning("Point entry {PointEntryId} was not deleted because it was not found.", id);
    }

    /// <summary>
    /// Sletter alle point-entries (bruges typisk til reset af point).
    /// </summary>
    /// <inheritdoc />
    public async Task DeleteAll()
    {
        await _points.DeleteManyAsync(_ => true);
    }

    // =========================
    // Helpers
    // =========================

    /// <summary>
    /// Finder næste Id til et nyt point-entry ved at hente den højeste eksisterende Id og lægge 1 til.
    /// </summary>
    /// <inheritdoc />
    public async Task<int> GetNextId()
    {
        // Bevar adfærd: find højeste Id ved at sortere desc og tage første.
        var last = await _points.Find(_ => true)
            .SortByDescending(p => p.Id)
            .Limit(1)
            .FirstOrDefaultAsync();

        return last != null ? last.Id + 1 : 1;
    }
}
