using Core;

namespace ServerAPI.Repositories.Highlights;

/// <summary>
/// Repository-kontrakt for highlights.
/// Her ligger highlights i en separat database-container ("highlights").
/// </summary>
public interface IHighlightRepository
{
    /// <summary>
    /// Henter alle highlights.
    /// Bruges typisk til admin/overblik – ellers foretrækkes paging.
    /// </summary>
    Task<IEnumerable<Highlight>> GetAll();

    /// <summary>
    /// Henter ét highlight ud fra id.
    /// Returnerer null hvis det ikke findes.
    /// </summary>
    Task<Highlight?> GetById(int id);

    /// <summary>
    /// Opretter et nyt highlight og returnerer det oprettede highlight.
    /// </summary>
    Task<Highlight> Add(Highlight highlight);

    /// <summary>
    /// Sletter et highlight ud fra id.
    /// </summary>
    Task<bool> Delete(int id);

    /// <summary>
    /// Opdaterer et highlight.
    /// </summary>
    Task<bool> Update(Highlight highlight);

    /// <summary>
    /// Henter highlights pagineret (server-side paging).
    /// page = 1 betyder første side.
    /// </summary>
    Task<PagedResult<Highlight>> GetPaged(
        int page,
        int pageSize,
        string? searchTerm = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        bool includePrivate = true);
}
