using Core;
using ServerAPI.Configuration;
using ServerAPI.Utils;

namespace ServerAPI.Repositories.Highlights;

/// <summary>
/// Cosmos DB implementation af highlight repository.
/// </summary>
public class HighlightRepositoryCosmosDb : IHighlightRepository
{
    private readonly CosmosDbContext _cosmos;
    private readonly ILogger<HighlightRepositoryCosmosDb> _logger;

    public HighlightRepositoryCosmosDb(CosmosDbContext cosmos, ILogger<HighlightRepositoryCosmosDb> logger)
    {
        _cosmos = cosmos;
        _logger = logger;
    }

    public async Task<IEnumerable<Highlight>> GetAll()
    {
        return await CosmosDbContext.ReadAllAsync<Highlight>(_cosmos.Highlights);
    }

    public async Task<Highlight?> GetById(int id)
    {
        return await CosmosDbContext.ReadByDocumentIdAsync<Highlight>(_cosmos.Highlights, id.ToString());
    }

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
        var query = (await CosmosDbContext.ReadAllAsync<Highlight>(_cosmos.Highlights)).AsEnumerable();

        if (!includePrivate)
            query = query.Where(h => !h.IsPrivate);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(h =>
                (h.Title ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)
                || (h.Description ?? "").Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (fromDate.HasValue)
            query = query.Where(h => h.Date >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(h => h.Date < toDate.Value.Date.AddDays(1));

        var filtered = query.ToList();
        var items = filtered
            .OrderByDescending(h => h.Date)
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        return new PagedResult<Highlight>(items, filtered.Count, page, pageSize);
    }

    public async Task<Highlight> Add(Highlight highlight)
    {
        if (highlight.Date == default)
            highlight.Date = DateTime.Today;

        highlight.Id = await _cosmos.GetNextIdAsync("highlights");

        TextAutoReplace.Apply(highlight, _logger);

        await CosmosDbContext.UpsertAsync(_cosmos.Highlights, highlight.Id.ToString(), highlight);
        return highlight;
    }

    public async Task<bool> Delete(int id)
    {
        var existing = await GetById(id);
        if (existing is null)
        {
            _logger.LogWarning("Highlight {HighlightId} was not deleted because it was not found.", id);
            return false;
        }

        await CosmosDbContext.DeleteAsync(_cosmos.Highlights, id.ToString());
        return true;
    }

    public async Task<bool> Update(Highlight highlight)
    {
        TextAutoReplace.Apply(highlight, _logger);

        var existing = await GetById(highlight.Id);
        if (existing is null)
        {
            _logger.LogWarning("Highlight {HighlightId} was not updated because it was not found.", highlight.Id);
            return false;
        }

        await CosmosDbContext.UpsertAsync(_cosmos.Highlights, highlight.Id.ToString(), highlight);
        return true;
    }
}
