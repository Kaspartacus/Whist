using Core;
using ServerAPI.Configuration;
using ServerAPI.Utils;

namespace ServerAPI.Repositories.Rules;

/// <summary>
/// Cosmos DB repository for regelsamlingen.
/// </summary>
public class RuleRepositoryCosmosDb : IRuleRepository
{
    private readonly CosmosDbContext _cosmos;
    private readonly ILogger<RuleRepositoryCosmosDb> _logger;

    public RuleRepositoryCosmosDb(CosmosDbContext cosmos, ILogger<RuleRepositoryCosmosDb> logger)
    {
        _cosmos = cosmos;
        _logger = logger;
    }

    public async Task<List<Rule>> GetAll()
        => await CosmosDbContext.ReadAllAsync<Rule>(_cosmos.Rules);

    public async Task<Rule?> GetById(int id)
        => await CosmosDbContext.ReadByDocumentIdAsync<Rule>(_cosmos.Rules, id.ToString());

    public async Task<Rule> Add(Rule rule)
    {
        var rules = await GetAll();
        var minimumNextRuleId = rules.Any() ? rules.Max(r => r.Id) + 1 : 1;
        rule.Id = await _cosmos.GetNextIdAtLeastAsync("rules", minimumNextRuleId);

        TextAutoReplace.Apply(rule, _logger);

        await CosmosDbContext.UpsertAsync(_cosmos.Rules, rule.Id.ToString(), rule);
        return rule;
    }

    public async Task<bool> Update(Rule rule)
    {
        TextAutoReplace.Apply(rule, _logger);

        var existing = await GetById(rule.Id);
        if (existing is null)
        {
            _logger.LogWarning("Rule {RuleId} was not updated because it was not found.", rule.Id);
            return false;
        }

        await CosmosDbContext.UpsertAsync(_cosmos.Rules, rule.Id.ToString(), rule);
        return true;
    }

    public async Task<bool> Delete(int id)
    {
        var existing = await GetById(id);
        if (existing is null)
        {
            _logger.LogWarning("Rule {RuleId} was not deleted because it was not found.", id);
            return false;
        }

        await CosmosDbContext.DeleteAsync(_cosmos.Rules, id.ToString());
        return true;
    }
}
