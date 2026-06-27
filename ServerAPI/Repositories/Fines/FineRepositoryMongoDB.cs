using Core;
using MongoDB.Driver;
using ServerAPI.Auth;
using ServerAPI.Configuration;
using ServerAPI.Utils;

namespace ServerAPI.Repositories.Fines;

/// <summary>
/// MongoDB implementation af IFineRepository.
/// Her ligger bøder som en "embedded list" på ApplicationUser-dokumentet:
///   ApplicationUser { Id, ..., List<Fine> Fines }
///
/// Fordel: simpelt for et lille projekt.
/// Ulempe: ved store datamængder kan updates blive tungere (read-modify-write på hele user-dokumentet).
/// </summary>
public class FineRepositoryMongoDB : IFineRepository
{
    private readonly IMongoCollection<ApplicationUser> _users;
    private readonly ILogger<FineRepositoryMongoDB> _logger;

    /// <summary>
    /// Opretter forbindelse til MongoDB baseret på appsettings.
    /// </summary>
    public FineRepositoryMongoDB(IConfiguration config, ILogger<FineRepositoryMongoDB> logger)
    {
        _logger = logger;
        var db = MongoDatabaseFactory.Create(config);
        _users = db.GetCollection<ApplicationUser>("users");
    }

    // =========================
    // READ
    // =========================

    /// <summary>
    /// Returnerer alle bøder på tværs af alle brugere (fladet ud) sorteret nyeste først.
    /// </summary>
    /// <inheritdoc />
    public Fine[] GetAll()
    {
        // Henter alle users og flader deres fines ud til én liste.
        // Sorterer nyeste først for en bedre UI-oplevelse.
        var allUsers = _users.Find(_ => true).ToList();

        return allUsers
            .SelectMany(u => u.Fines)
            .OrderByDescending(f => f.Date)
            .ToArray();
    }

    /// <summary>
    /// Returnerer alle bøder for én bruger (sorteret nyeste først).
    /// </summary>
    /// <inheritdoc />
    public Fine[] GetByUserId(int userId)
    {
        // Find én bruger og returnér deres fines (sorteret nyeste først).
        var user = _users.Find(u => u.Id == userId).FirstOrDefault();

        return user?.Fines
                   .OrderByDescending(f => f.Date)
                   .ToArray()
               ?? Array.Empty<Fine>();
    }

    // =========================
    // CREATE
    // =========================

    /// <summary>
    /// Tilføjer en ny bøde til den relevante bruger (embedded i User-dokumentet) og gemmer brugeren igen.
    /// </summary>
    /// <inheritdoc />
    public void AddFine(Fine fine)
    {
        // Find brugeren som bøden tilhører.
        var user = _users.Find(u => u.Id == fine.UserId).FirstOrDefault();
        if (user == null)
        {
            _logger.LogWarning("Fine was not created because user {UserId} was not found.", fine.UserId);
            return;
        }

        // ID genereres som "max + 1" indenfor brugerens fines.
        // (Fungerer fint for jeres use case med få brugere)
        fine.Id = user.Fines.Any() ? user.Fines.Max(f => f.Id) + 1 : 1;

        // Bøder får "nu" dato ved oprettelse (uanset hvad klienten sender).
        fine.Date = DateTime.Now;

        // Automatisk: erstatter "KSDH" med BIF<3.
        TextAutoReplace.Apply(fine, _logger);
        
        user.Fines.Add(fine);

        // Når fines ligger embedded i user, erstatter vi hele user dokumentet.
        _users.ReplaceOne(u => u.Id == user.Id, user);
    }

    // =========================
    // UPDATE
    // =========================

    /// <summary>
    /// Opdaterer en eksisterende bøde ved at erstatte elementet i brugerens fines-liste og gemme brugeren igen.
    /// </summary>
    /// <inheritdoc />
    public void Update(Fine fine)
    {
        // Find brugeren og opdater bøden i deres fines-liste.
        var user = _users.Find(u => u.Id == fine.UserId).FirstOrDefault();
        if (user == null)
        {
            _logger.LogWarning("Fine {FineId} was not updated because user {UserId} was not found.", fine.Id, fine.UserId);
            return;
        }

        var finesList = user.Fines.ToList();
        var index = finesList.FindIndex(f => f.Id == fine.Id);

        if (index >= 0)
        {
            // Automatisk: erstatter "KSDH" med BIF<3.
            TextAutoReplace.Apply(fine, _logger);
            
            finesList[index] = fine;
            user.Fines = finesList;

            _users.ReplaceOne(u => u.Id == user.Id, user);
        }
        else
        {
            _logger.LogWarning("Fine {FineId} was not updated because it was not found for user {UserId}.", fine.Id, fine.UserId);
        }
    }

    // =========================
    // DELETE
    // =========================

    /// <summary>
    /// Sletter en bøde fra en bestemt bruger ved at filtrere bøden ud af fines-listen og gemme brugeren igen.
    /// </summary>
    /// <inheritdoc />
    public void Delete(int userId, int id)
    {
        // Find brugeren og fjern bøden fra deres liste.
        var user = _users.Find(u => u.Id == userId).FirstOrDefault();
        if (user == null)
        {
            _logger.LogWarning("Fine {FineId} was not deleted because user {UserId} was not found.", id, userId);
            return;
        }

        var beforeCount = user.Fines.Count;

        user.Fines = user.Fines.Where(f => f.Id != id).ToList();
        if (user.Fines.Count == beforeCount)
            _logger.LogWarning("Fine {FineId} was not deleted because it was not found for user {UserId}.", id, userId);

        _users.ReplaceOne(u => u.Id == user.Id, user);
    }

    // =========================
    // PAGING
    // =========================

    /// <summary>
    /// Wrapper-type som MongoDB bruger ved Unwind på embedded array.
    /// Når vi Unwind'er User.Fines, kommer hvert element ud som en wrapper med property "Fines".
    /// </summary>
    public class FineWrapper
    {
        public Fine Fines { get; set; } = default!;
    }

    /// <summary>
    /// Henter bøder pagineret med valgfri filtrering (bruger, søgning, dato, beløb og betalt-status) via aggregation pipeline.
    /// </summary>
    /// <inheritdoc />
    public PagedResult<Fine> GetPaged(int page, int pageSize, int? userId = null, string? searchTerm = null, DateTime? fromDate = null, DateTime? toDate = null, decimal? minAmount = null, decimal? maxAmount = null, bool? isPaid = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 5;

        var skip = (page - 1) * pageSize;

        var userFilter = userId.HasValue
            ? Builders<ApplicationUser>.Filter.Eq(u => u.Id, userId.Value)
            : Builders<ApplicationUser>.Filter.Empty;

        // 1) Hvis vi søger på navn, så find userIds der matcher (case-insensitive)
        HashSet<int>? matchedUserIds = null;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLowerInvariant();

            matchedUserIds = _users.Find(_ => true)
                .Project(u => new { u.Id, u.NickName })
                .ToList()
                .Where(u => (u.NickName ?? "").ToLowerInvariant().Contains(term))
                .Select(u => u.Id)
                .ToHashSet();
        }

        // 2) Byg fine-level filter (efter unwind)
        var fineFilters = new List<FilterDefinition<FineWrapper>>();

        // Paid filter
        if (isPaid.HasValue)
            fineFilters.Add(Builders<FineWrapper>.Filter.Eq(f => f.Fines.IsPaid, isPaid.Value));

        // Date range (inklusive)
        if (fromDate.HasValue)
            fineFilters.Add(Builders<FineWrapper>.Filter.Gte(f => f.Fines.Date, fromDate.Value.Date));

        if (toDate.HasValue)
            fineFilters.Add(Builders<FineWrapper>.Filter.Lt(f => f.Fines.Date, toDate.Value.Date.AddDays(1)));

        // Amount range
        if (minAmount.HasValue)
            fineFilters.Add(Builders<FineWrapper>.Filter.Gte(f => f.Fines.Amount, minAmount.Value));

        if (maxAmount.HasValue)
            fineFilters.Add(Builders<FineWrapper>.Filter.Lte(f => f.Fines.Amount, maxAmount.Value));

        // SearchTerm: comment contains OR userId in matchedUserIds
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();

            var commentMatch = Builders<FineWrapper>.Filter.Regex(
                f => f.Fines.Comment,
                new MongoDB.Bson.BsonRegularExpression(term, "i"));

            var orParts = new List<FilterDefinition<FineWrapper>> { commentMatch };

            if (matchedUserIds is { Count: > 0 })
            {
                var userIdMatch = Builders<FineWrapper>.Filter.In(f => f.Fines.UserId, matchedUserIds);
                orParts.Add(userIdMatch);
            }

            fineFilters.Add(Builders<FineWrapper>.Filter.Or(orParts));
        }

        var fineFilter = fineFilters.Count == 0
            ? Builders<FineWrapper>.Filter.Empty
            : Builders<FineWrapper>.Filter.And(fineFilters);

        // 3) TotalCount: count efter samme filtre
        var totalCount = _users.Aggregate()
            .Match(userFilter)
            .Unwind<ApplicationUser, FineWrapper>(u => u.Fines)
            .Match(fineFilter)
            .Count()
            .FirstOrDefault()
            ?.Count ?? 0;

        // 4) Items: match + sort + skip + limit
        var items = _users.Aggregate()
            .Match(userFilter)
            .Unwind<ApplicationUser, FineWrapper>(u => u.Fines)
            .Match(fineFilter)
            .SortByDescending(fw => fw.Fines.Date)
            .Skip(skip)
            .Limit(pageSize)
            .Project(fw => fw.Fines)
            .ToList();

        return new PagedResult<Fine>(items, (int)totalCount, page, pageSize);
    }
}
