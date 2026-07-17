using Core;
using ServerAPI.Auth;
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

    public async Task<bool> Add(PointEntry point)
    {
        var user = await CosmosDbContext.ReadByDocumentIdAsync<ApplicationUser>(_cosmos.Users, point.PlayerId.ToString());
        if (user is null)
        {
            _logger.LogWarning("Point entry was not created because player {PlayerId} was not found.", point.PlayerId);
            return false;
        }

        var points = await GetAll();
        var minimumNextPointId = points.Any() ? points.Max(p => p.Id) + 1 : 1;
        point.Id = await _cosmos.GetNextIdAtLeastAsync("points", minimumNextPointId);
        await CosmosDbContext.UpsertAsync(_cosmos.Points, point.Id.ToString(), point);
        return true;
    }

    public async Task<bool> Delete(int id)
    {
        var existing = await CosmosDbContext.ReadByDocumentIdAsync<PointEntry>(_cosmos.Points, id.ToString());
        if (existing is null)
        {
            _logger.LogWarning("Point entry {PointEntryId} was not deleted because it was not found.", id);
            return false;
        }

        await CosmosDbContext.DeleteAsync(_cosmos.Points, id.ToString());
        return true;
    }

    public async Task DeleteAll()
    {
        var points = await GetAll();
        foreach (var point in points)
            await CosmosDbContext.DeleteAsync(_cosmos.Points, point.Id.ToString());
    }

}
