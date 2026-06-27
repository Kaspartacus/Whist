using Microsoft.AspNetCore.Identity;
using ServerAPI.Configuration;

namespace ServerAPI.Auth;

public sealed class CosmosRoleStore : IRoleStore<ApplicationRole>
{
    private readonly CosmosDbContext _cosmos;

    public CosmosRoleStore(CosmosDbContext cosmos) => _cosmos = cosmos;

    public void Dispose() { }

    public async Task<IdentityResult> CreateAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        if (role.Id == 0)
        {
            var lastRole = (await CosmosDbContext.ReadAllAsync<ApplicationRole>(_cosmos.Roles, cancellationToken))
                .OrderByDescending(item => item.Id)
                .FirstOrDefault();
            role.Id = (lastRole?.Id ?? 0) + 1;
        }

        await CosmosDbContext.UpsertAsync(_cosmos.Roles, role.Id.ToString(), role, cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        await CosmosDbContext.UpsertAsync(_cosmos.Roles, role.Id.ToString(), role, cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        await CosmosDbContext.DeleteAsync(_cosmos.Roles, role.Id.ToString(), cancellationToken);
        return IdentityResult.Success;
    }

    public Task<string> GetRoleIdAsync(ApplicationRole role, CancellationToken cancellationToken)
        => Task.FromResult(role.Id.ToString());

    public Task<string?> GetRoleNameAsync(ApplicationRole role, CancellationToken cancellationToken)
        => Task.FromResult(role.Name);

    public Task SetRoleNameAsync(ApplicationRole role, string? roleName, CancellationToken cancellationToken)
    {
        role.Name = roleName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedRoleNameAsync(ApplicationRole role, CancellationToken cancellationToken)
        => Task.FromResult(role.NormalizedName);

    public Task SetNormalizedRoleNameAsync(ApplicationRole role, string? normalizedName, CancellationToken cancellationToken)
    {
        role.NormalizedName = normalizedName;
        return Task.CompletedTask;
    }

    public async Task<ApplicationRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(roleId, out var id))
            return null;

        return await CosmosDbContext.ReadByDocumentIdAsync<ApplicationRole>(_cosmos.Roles, id.ToString(), cancellationToken);
    }

    public async Task<ApplicationRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
        => (await CosmosDbContext.ReadAllAsync<ApplicationRole>(_cosmos.Roles, cancellationToken))
            .FirstOrDefault(role => role.NormalizedName == normalizedRoleName);
}
