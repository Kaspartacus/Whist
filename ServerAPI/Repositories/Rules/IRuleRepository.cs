using Core;

namespace ServerAPI.Repositories.Rules;

/// <summary>
/// Repository-kontrakt for regler.
/// 
/// Formål:
/// - Skjule persistence fra controller.
/// - Give et simpelt CRUD API for Rule.
/// </summary>
public interface IRuleRepository
{
    /// <summary>Henter alle regler.</summary>
    Task<List<Rule>> GetAll();

    /// <summary>Henter en enkelt regel på id.</summary>
    Task<Rule?> GetById(int id);

    /// <summary>Opretter en ny regel og returnerer den (med sat Id).</summary>
    Task<Rule> Add(Rule rule);

    /// <summary>Opdaterer en eksisterende regel (samme Id).</summary>
    Task<bool> Update(Rule rule);

    /// <summary>Sletter en regel ud fra id.</summary>
    Task<bool> Delete(int id);
}
