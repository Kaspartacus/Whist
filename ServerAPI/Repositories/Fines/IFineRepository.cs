using Core;

namespace ServerAPI.Repositories.Fines;

/// <summary>
/// Repository-kontrakt for bøder.
/// I denne løsning ligger bøder som en liste på User-dokumentet i databasen.
/// </summary>
public interface IFineRepository
{
    /// <summary>
    /// Henter alle bøder (på tværs af brugere).
    /// </summary>
    Task<Fine[]> GetAll(bool includeArchived = false);

    /// <summary>
    /// Henter bøder for én bruger.
    /// </summary>
    Task<Fine[]> GetByUserId(int userId, bool includeArchived = false);

    /// <summary>
    /// Tilføjer en ny bøde til en bruger.
    /// </summary>
    Task<bool> AddFine(Fine fine);

    /// <summary>
    /// Opdaterer en eksisterende bøde (fx markering som betalt).
    /// </summary>
    Task<bool> Update(Fine fine);

    /// <summary>
    /// Sletter en bøde for en given bruger.
    /// </summary>
    Task<bool> Delete(int userId, int id);

    /// <summary>
    /// Henter bøder pagineret. Kan filtrere på userId.
    /// </summary>
    Task<PagedResult<Fine>> GetPaged(
        int page,
        int pageSize,
        int? userId = null,
        string? searchTerm = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        bool? isPaid = null,
        bool? isArchived = null);
}
