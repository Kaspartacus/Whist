using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

namespace ServerAPI.Auth;

public sealed class MongoRoleStore : IRoleStore<ApplicationRole>
{
    private readonly IMongoCollection<ApplicationRole> _roles;

    public MongoRoleStore(MongoIdentityContext context) => _roles = context.Roles;

    public void Dispose() { }

    public async Task<IdentityResult> CreateAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        if (role.Id == 0)
        {
            var lastRole = await _roles.Find(_ => true).SortByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken);
            role.Id = (lastRole?.Id ?? 0) + 1;
        }

        await _roles.InsertOneAsync(role, cancellationToken: cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        await _roles.ReplaceOneAsync(item => item.Id == role.Id, role, cancellationToken: cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        await _roles.DeleteOneAsync(item => item.Id == role.Id, cancellationToken);
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

        return await _roles.Find(role => role.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ApplicationRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
        => await _roles.Find(role => role.NormalizedName == normalizedRoleName).FirstOrDefaultAsync(cancellationToken);
}
