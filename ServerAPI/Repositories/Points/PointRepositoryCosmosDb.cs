using Core;
using ServerAPI.Configuration;

namespace ServerAPI.Repositories.Points;

/// <summary>
/// Cosmos DB implementation af points repository.
/// </summary>
public class PointRepositoryCosmosDb : IPointRepository
{
    private readonly CosmosDbContext _cosmos;
    private readonly ILogger<PointRepositoryCosmosDb> _logger;

    public PointRepositoryCosmosDb(CosmosDbContext cosmos, ILogger<PointRepositoryCosmosDb> logger)
    {
        _cosmos = cosmos;
        _logger = logger;
    }

    public async Task<List<PointEntry>> GetAll()
    {
        return await CosmosDbContext.ReadAllAsync<PointEntry>(_cosmos.Points);
    }

    public async Task Add(PointEntry point)
    {
        var points = await GetAll();
        var minimumNextPointId = points.Any() ? points.Max(p => p.Id) + 1 : 1;
        point.Id = await _cosmos.GetNextIdAtLeastAsync("points", minimumNextPointId);
        await CosmosDbContext.UpsertAsync(_cosmos.Points, point.Id.ToString(), point);
    }

    public async Task Delete(int id)
    {
        var existing = await CosmosDbContext.ReadByDocumentIdAsync<PointEntry>(_cosmos.Points, id.ToString());
        if (existing is null)
        {
            _logger.LogWarning("Point entry {PointEntryId} was not deleted because it was not found.", id);
            return;
        }

        await CosmosDbContext.DeleteAsync(_cosmos.Points, id.ToString());
    }

    public async Task DeleteAll()
    {
        var points = await GetAll();
        foreach (var point in points)
            await CosmosDbContext.DeleteAsync(_cosmos.Points, point.Id.ToString());
    }

}
