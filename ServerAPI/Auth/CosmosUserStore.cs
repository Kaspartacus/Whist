using Microsoft.AspNetCore.Identity;
using ServerAPI.Configuration;

namespace ServerAPI.Auth;

public sealed class CosmosUserStore :
    IUserStore<ApplicationUser>,
    IUserPasswordStore<ApplicationUser>,
    IUserEmailStore<ApplicationUser>,
    IUserSecurityStampStore<ApplicationUser>,
    IUserRoleStore<ApplicationUser>,
    IUserLockoutStore<ApplicationUser>
{
    private readonly CosmosDbContext _cosmos;

    public CosmosUserStore(CosmosDbContext cosmos) => _cosmos = cosmos;

    public void Dispose() { }

    public async Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await CosmosDbContext.UpsertAsync(_cosmos.Users, user.Id.ToString(), user, cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var existing = await FindByIdAsync(user.Id.ToString(), cancellationToken);
        if (existing is null)
            return IdentityResult.Failed(new IdentityError { Code = "UserNotFound", Description = "User was not found." });

        await CosmosDbContext.UpsertAsync(_cosmos.Users, user.Id.ToString(), user, cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await CosmosDbContext.DeleteAsync(_cosmos.Users, user.Id.ToString(), cancellationToken);
        return IdentityResult.Success;
    }

    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.Id.ToString());

    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.UserName);

    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
    {
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public async Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(userId, out var id))
            return null;

        return await CosmosDbContext.ReadByDocumentIdAsync<ApplicationUser>(_cosmos.Users, id.ToString(), cancellationToken);
    }

    public async Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        => (await CosmosDbContext.ReadAllAsync<ApplicationUser>(_cosmos.Users, cancellationToken))
            .FirstOrDefault(user => user.NormalizedUserName == normalizedUserName);

    public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.PasswordHash);

    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(!string.IsNullOrWhiteSpace(user.PasswordHash));

    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.Email);

    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.EmailConfirmed);

    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public async Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        => (await CosmosDbContext.ReadAllAsync<ApplicationUser>(_cosmos.Users, cancellationToken))
            .FirstOrDefault(user => user.NormalizedEmail == normalizedEmail);

    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    public Task SetSecurityStampAsync(ApplicationUser user, string stamp, CancellationToken cancellationToken)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.SecurityStamp);

    public Task AddToRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        var canonicalRole = CanonicalRole(roleName);
        if (!user.Roles.Contains(canonicalRole, StringComparer.OrdinalIgnoreCase))
            user.Roles.Add(canonicalRole);
        return Task.CompletedTask;
    }

    public Task RemoveFromRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        user.Roles.RemoveAll(role => string.Equals(role, CanonicalRole(roleName), StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult<IList<string>>(user.Roles.ToList());

    public Task<bool> IsInRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
        => Task.FromResult(user.Roles.Contains(CanonicalRole(roleName), StringComparer.OrdinalIgnoreCase));

    public async Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
        => (await CosmosDbContext.ReadAllAsync<ApplicationUser>(_cosmos.Users, cancellationToken))
            .Where(user => user.Roles.Contains(CanonicalRole(roleName), StringComparer.OrdinalIgnoreCase))
            .ToList();

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.LockoutEnd);

    public Task SetLockoutEndDateAsync(ApplicationUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(++user.AccessFailedCount);

    public Task ResetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.AccessFailedCount);

    public Task<bool> GetLockoutEnabledAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.LockoutEnabled);

    public Task SetLockoutEnabledAsync(ApplicationUser user, bool enabled, CancellationToken cancellationToken)
    {
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    private static string CanonicalRole(string roleName)
        => roleName.ToUpperInvariant() switch
        {
            "ADMIN" => "Admin",
            "MEMBER" => "Member",
            _ => roleName
        };
}
