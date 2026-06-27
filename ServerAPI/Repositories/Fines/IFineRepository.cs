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
    Fine[] GetAll();

    /// <summary>
    /// Henter bøder for én bruger.
    /// </summary>
    Fine[] GetByUserId(int userId);

    /// <summary>
    /// Tilføjer en ny bøde til en bruger.
    /// </summary>
    void AddFine(Fine fine);

    /// <summary>
    /// Opdaterer en eksisterende bøde (fx markering som betalt).
    /// </summary>
    void Update(Fine fine);

    /// <summary>
    /// Sletter en bøde for en given bruger.
    /// </summary>
    void Delete(int userId, int id);

    /// <summary>
    /// Henter bøder pagineret. Kan filtrere på userId.
    /// </summary>
    PagedResult<Fine> GetPaged(
        int page,
        int pageSize,
        int? userId = null,
        string? searchTerm = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        bool? isPaid = null);
}
