using Core;

namespace ServerAPI.Repositories.Points;

/// <summary>
/// Repository-kontrakt for points.
/// Ansvar:
/// - Abstrahere persistence væk fra controlleren
/// - Holde et simpelt API til CRUD på PointEntry
/// </summary>
public interface IPointRepository
{
    /// <summary>
    /// Henter alle point entries.
    /// </summary>
    Task<List<PointEntry>> GetAll();

    /// <summary>
    /// Opretter en ny point entry.
    /// </summary>
    Task Add(PointEntry point);

    /// <summary>
    /// Sletter en point entry ud fra id.
    /// </summary>
    Task Delete(int id);

    /// <summary>
    /// Finder næste sekventielle id (1,2,3,...).
    /// NOTE: Dette matcher din nuværende løsning (sortér på Id, tag største, +1).
    /// </summary>
    Task<int> GetNextId();

    /// <summary>
    /// Sletter alle points (nulstil).
    /// </summary>
    Task DeleteAll();
}
