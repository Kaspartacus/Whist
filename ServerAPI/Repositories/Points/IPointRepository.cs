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
    Task<bool> Add(PointEntry point);

    /// <summary>
    /// Sletter en point entry ud fra id.
    /// </summary>
    Task<bool> Delete(int id);

    /// <summary>
    /// Sletter alle points (nulstil).
    /// </summary>
    Task DeleteAll();
}
