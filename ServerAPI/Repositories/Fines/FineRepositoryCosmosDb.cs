using Core;
using ServerAPI.Auth;
using ServerAPI.Configuration;
using ServerAPI.Utils;

namespace ServerAPI.Repositories.Fines;

/// <summary>
/// Cosmos DB implementation af IFineRepository.
/// Bøder ligger fortsat embedded på ApplicationUser-dokumentet, så API-adfærden er uændret.
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

    public async Task<Fine[]> GetAll()
    {
        var allUsers = await CosmosDbContext.ReadAllAsync<ApplicationUser>(_cosmos.Users);

        return allUsers
            .SelectMany(u => u.Fines)
            .OrderByDescending(f => f.Date)
            .ToArray();
    }

    public async Task<Fine[]> GetByUserId(int userId)
    {
        var user = await CosmosDbContext.ReadByDocumentIdAsync<ApplicationUser>(_cosmos.Users, userId.ToString());

        return user?.Fines
                   .OrderByDescending(f => f.Date)
                   .ToArray()
               ?? Array.Empty<Fine>();
    }

    public async Task AddFine(Fine fine)
    {
        var user = await CosmosDbContext.ReadByDocumentIdAsync<ApplicationUser>(_cosmos.Users, fine.UserId.ToString());

        if (user == null)
        {
            _logger.LogWarning("Fine was not created because user {UserId} was not found.", fine.UserId);
            return;
        }

        fine.Id = user.Fines.Any() ? user.Fines.Max(f => f.Id) + 1 : 1;
        fine.Date = DateTime.Now;

        TextAutoReplace.Apply(fine, _logger);

        user.Fines.Add(fine);

        await CosmosDbContext.UpsertAsync(_cosmos.Users, user.Id.ToString(), user);
    }

    public async Task Update(Fine fine)
    {
        var user = await CosmosDbContext.ReadByDocumentIdAsync<ApplicationUser>(_cosmos.Users, fine.UserId.ToString());

        if (user == null)
        {
            _logger.LogWarning("Fine {FineId} was not updated because user {UserId} was not found.", fine.Id, fine.UserId);
            return;
        }

        var finesList = user.Fines.ToList();
        var index = finesList.FindIndex(f => f.Id == fine.Id);

        if (index >= 0)
        {
            TextAutoReplace.Apply(fine, _logger);
            finesList[index] = fine;
            user.Fines = finesList;

            await CosmosDbContext.UpsertAsync(_cosmos.Users, user.Id.ToString(), user);
        }
        else
        {
            _logger.LogWarning("Fine {FineId} was not updated because it was not found for user {UserId}.", fine.Id, fine.UserId);
        }
    }

    public async Task Delete(int userId, int id)
    {
        var user = await CosmosDbContext.ReadByDocumentIdAsync<ApplicationUser>(_cosmos.Users, userId.ToString());

        if (user == null)
        {
            _logger.LogWarning("Fine {FineId} was not deleted because user {UserId} was not found.", id, userId);
            return;
        }

        var beforeCount = user.Fines.Count;

        user.Fines = user.Fines.Where(f => f.Id != id).ToList();
        if (user.Fines.Count == beforeCount)
            _logger.LogWarning("Fine {FineId} was not deleted because it was not found for user {UserId}.", id, userId);

        await CosmosDbContext.UpsertAsync(_cosmos.Users, user.Id.ToString(), user);
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
        bool? isPaid = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 5;

        var skip = (page - 1) * pageSize;

        var users = await CosmosDbContext.ReadAllAsync<ApplicationUser>(_cosmos.Users);

        if (userId.HasValue)
            users = users.Where(u => u.Id == userId.Value).ToList();

        var userNamesById = users.ToDictionary(u => u.Id, u => u.NickName ?? "");
        var query = users.SelectMany(u => u.Fines).AsEnumerable();

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
}
