using Core;
using ServerAPI.Auth;
using ServerAPI.Configuration;
using ServerAPI.Utils;

namespace ServerAPI.Repositories.Fines;

/// <summary>
/// Cosmos DB implementation af IFineRepository.
/// Bøder gemmes i egen container.
/// </summary>
public class FineRepositoryCosmosDb : IFineRepository
{
    private readonly CosmosDbContext _cosmos;
    private readonly ILogger<FineRepositoryCosmosDb> _logger;

    public FineRepositoryCosmosDb(CosmosDbContext cosmos, ILogger<FineRepositoryCosmosDb> logger)
    {
        _cosmos = cosmos;
        _logger = logger;
    }

    public async Task<Fine[]> GetAll(bool includeArchived = false)
    {
        var fines = await ReadFinesWithArchiveStateAsync();
        return fines
            .Where(f => includeArchived || !f.IsArchived)
            .OrderByDescending(f => f.Date)
            .ToArray();
    }

    public async Task<Fine[]> GetByUserId(int userId, bool includeArchived = false)
    {
        var fines = await ReadFinesWithArchiveStateAsync();
        return fines
            .Where(f => f.UserId == userId)
            .Where(f => includeArchived || !f.IsArchived)
            .OrderByDescending(f => f.Date)
            .ToArray();
    }

    public async Task<bool> AddFine(Fine fine)
    {
        var user = await CosmosDbContext.ReadByDocumentIdAsync<ApplicationUser>(_cosmos.Users, fine.UserId.ToString());

        if (user == null)
        {
            _logger.LogWarning("Fine was not created because user {UserId} was not found.", fine.UserId);
            return false;
        }

        var userFines = await GetByUserId(fine.UserId, includeArchived: true);
        fine.Id = userFines.Any() ? userFines.Max(f => f.Id) + 1 : 1;
        fine.Date = DateTime.Now;
        fine.PaidAt = fine.IsPaid ? DateTime.UtcNow : null;
        fine.IsArchived = false;
        fine.ArchivedAt = null;
        FineArchivePolicy.Apply(fine, DateTime.UtcNow);

        TextAutoReplace.Apply(fine, _logger);

        await CosmosDbContext.UpsertAsync(_cosmos.Fines, FineDocumentId(fine), fine);
        return true;
    }

    public async Task<bool> Update(Fine fine)
    {
        var existing = await CosmosDbContext.ReadByDocumentIdAsync<Fine>(_cosmos.Fines, FineDocumentId(fine.UserId, fine.Id));

        if (existing is null)
        {
            _logger.LogWarning("Fine {FineId} was not updated because it was not found for user {UserId}.", fine.Id, fine.UserId);
            return false;
        }

        fine.Date = fine.Date == default ? existing.Date : fine.Date;
        fine.PaidAt = fine.IsPaid
            ? existing.PaidAt ?? DateTime.UtcNow
            : null;
        FineArchivePolicy.Apply(fine, DateTime.UtcNow);

        TextAutoReplace.Apply(fine, _logger);

        await CosmosDbContext.UpsertAsync(_cosmos.Fines, FineDocumentId(fine), fine);
        return true;
    }

    public async Task<bool> Delete(int userId, int id)
    {
        var existing = await CosmosDbContext.ReadByDocumentIdAsync<Fine>(_cosmos.Fines, FineDocumentId(userId, id));
        if (existing is null)
        {
            _logger.LogWarning("Fine {FineId} was not deleted because it was not found for user {UserId}.", id, userId);
            return false;
        }

        await CosmosDbContext.DeleteAsync(_cosmos.Fines, FineDocumentId(userId, id));
        return true;
    }

    public async Task<PagedResult<Fine>> GetPaged(
        int page,
        int pageSize,
        int? userId = null,
        string? searchTerm = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        bool? isPaid = null,
        bool? isArchived = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 5;

        var skip = (page - 1) * pageSize;

        var users = await CosmosDbContext.ReadAllAsync<ApplicationUser>(_cosmos.Users);
        var fines = await ReadFinesWithArchiveStateAsync();

        if (userId.HasValue)
            fines = fines.Where(f => f.UserId == userId.Value).ToList();

        var userNamesById = users.ToDictionary(u => u.Id, u => u.NickName ?? "");
        var query = fines.AsEnumerable();

        if (isArchived.HasValue)
            query = query.Where(f => f.IsArchived == isArchived.Value);
        else
            query = query.Where(f => !f.IsArchived);

        if (isPaid.HasValue)
            query = query.Where(f => f.IsPaid == isPaid.Value);

        if (fromDate.HasValue)
            query = query.Where(f => f.Date >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(f => f.Date < toDate.Value.Date.AddDays(1));

        if (minAmount.HasValue)
            query = query.Where(f => f.Amount >= minAmount.Value);

        if (maxAmount.HasValue)
            query = query.Where(f => f.Amount <= maxAmount.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();

            query = query.Where(f =>
                (f.Comment ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)
                || (userNamesById.TryGetValue(f.UserId, out var name)
                    && name.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var filtered = query.ToList();
        var items = filtered
            .OrderByDescending(f => f.Date)
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        return new PagedResult<Fine>(items, filtered.Count, page, pageSize);
    }

    private static string FineDocumentId(Fine fine)
        => FineDocumentId(fine.UserId, fine.Id);

    private static string FineDocumentId(int userId, int fineId)
        => $"{userId}:{fineId}";

    private async Task<List<Fine>> ReadFinesWithArchiveStateAsync()
    {
        var fines = await CosmosDbContext.ReadAllAsync<Fine>(_cosmos.Fines);
        var now = DateTime.UtcNow;
        var changedFines = fines.Where(fine => FineArchivePolicy.Apply(fine, now)).ToList();

        foreach (var fine in changedFines)
            await CosmosDbContext.UpsertAsync(_cosmos.Fines, FineDocumentId(fine), fine);

        if (changedFines.Count > 0)
            _logger.LogInformation("Updated archive state for {FineCount} fines.", changedFines.Count);

        return fines;
    }
}
