using Core;
using MongoDB.Driver;
using ServerAPI.Configuration;
using ServerAPI.Utils;

namespace ServerAPI.Repositories.Rules;

/// <summary>
/// MongoDB repository for regelsamlingen.
/// 
/// Collection: "rules"
/// Bemærk:
/// - Vi bruger et manuelt heltals-Id (ikke Mongo _id/ObjectId).
/// - Add() finder "højeste Id" og sætter næste Id.
///   Det er OK for jeres use case (få rules, sjældne ændringer).
/// </summary>
public class RuleRepositoryMongoDB : IRuleRepository
{
    private const string CollectionName = "rules";

    private readonly IMongoCollection<Rule> _rules;
    private readonly ILogger<RuleRepositoryMongoDB> _logger;

    /// <summary>
    /// Opretter forbindelse til MongoDB baseret på appsettings og initialiserer rules-collection.
    /// </summary>
    public RuleRepositoryMongoDB(IConfiguration config, ILogger<RuleRepositoryMongoDB> logger)
    {
        _logger = logger;
        var db = MongoDatabaseFactory.Create(config);
        _rules = db.GetCollection<Rule>(CollectionName);
    }

    // =========================
    // READ
    // =========================

    /// <summary>
    /// Returnerer alle regler fra databasen.
    /// </summary>
    public async Task<List<Rule>> GetAll()
        => await _rules.Find(_ => true).ToListAsync();

    /// <summary>
    /// Finder én regel ud fra Id (returnerer null hvis den ikke findes).
    /// </summary>
    public async Task<Rule?> GetById(int id)
        => await _rules.Find(r => r.Id == id).FirstOrDefaultAsync();

    // =========================
    // WRITE
    // =========================

    /// <summary>
    /// Opretter en ny regel ved at finde næste ledige Id og indsætte reglen i databasen.
    /// </summary>
    public async Task<Rule> Add(Rule rule)
    {
        // Find næste ledige Id (manuelt, da vi ikke bruger ObjectId).
        // For få regler er dette helt fint og kræver minimal vedligehold.
        var highest = await _rules.Find(_ => true)
            .SortByDescending(r => r.Id)
            .FirstOrDefaultAsync();

        rule.Id = highest?.Id + 1 ?? 1;
        
        // Automatisk: erstatter "KSDH" med BIF<3.
        TextAutoReplace.Apply(rule, _logger);

        await _rules.InsertOneAsync(rule);
        return rule;
    }

    /// <summary>
    /// Opdaterer en eksisterende regel ved at erstatte dokumentet med samme Id.
    /// </summary>
    public async Task Update(Rule rule)
    {
        // Automatisk: erstatter "KSDH" med BIF<3.
        TextAutoReplace.Apply(rule, _logger);
        
        var result = await _rules.ReplaceOneAsync(r => r.Id == rule.Id, rule);
        if (result.MatchedCount == 0)
            _logger.LogWarning("Rule {RuleId} was not updated because it was not found.", rule.Id);
    }

    /// <summary>
    /// Sletter en regel ud fra Id.
    /// </summary>
    public async Task Delete(int id)
    {
        var result = await _rules.DeleteOneAsync(r => r.Id == id);
        if (result.DeletedCount == 0)
            _logger.LogWarning("Rule {RuleId} was not deleted because it was not found.", id);
    }
}
