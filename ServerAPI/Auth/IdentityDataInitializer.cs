using Microsoft.AspNetCore.Identity;
using ServerAPI.Configuration;

namespace ServerAPI.Auth;

public sealed class IdentityDataInitializer
{
    private static readonly string[] RequiredRoles = ["Member", "Admin"];

    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CosmosDbContext _cosmos;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdentityDataInitializer> _logger;

    public IdentityDataInitializer(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        CosmosDbContext cosmos,
        IConfiguration configuration,
        ILogger<IdentityDataInitializer> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _cosmos = cosmos;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var adminEmails = GetConfiguredAdminEmails();

        foreach (var roleName in RequiredRoles)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var result = await _roleManager.CreateAsync(new ApplicationRole { Name = roleName });
                EnsureSuccess(result, $"create role '{roleName}'");
            }
        }

        var users = await CosmosDbContext.ReadAllAsync<ApplicationUser>(_cosmos.Users, cancellationToken);
        foreach (var user in users)
        {
            var changed = false;

            if (string.IsNullOrWhiteSpace(user.UserName))
            {
                user.UserName = user.Email;
                changed = true;
            }

            var normalizedEmail = _userManager.NormalizeEmail(user.Email);
            var normalizedUserName = _userManager.NormalizeName(user.UserName);
            if (user.NormalizedEmail != normalizedEmail)
            {
                user.NormalizedEmail = normalizedEmail;
                changed = true;
            }

            if (user.NormalizedUserName != normalizedUserName)
            {
                user.NormalizedUserName = normalizedUserName;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(user.SecurityStamp))
            {
                user.SecurityStamp = Guid.NewGuid().ToString();
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(user.ConcurrencyStamp))
            {
                user.ConcurrencyStamp = Guid.NewGuid().ToString();
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
                throw new InvalidOperationException($"User {user.Id} has no password hash. Set a password before starting the API.");

            if (!user.Roles.Contains("Member", StringComparer.OrdinalIgnoreCase))
            {
                user.Roles.Add("Member");
                changed = true;
            }

            var shouldBeAdmin = IsConfiguredAdmin(user, adminEmails);
            var isAdmin = user.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
            if (shouldBeAdmin && !isAdmin)
            {
                user.Roles.Add("Admin");
                changed = true;
            }
            else if (!shouldBeAdmin && isAdmin)
            {
                user.Roles.RemoveAll(role => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase));
                user.SecurityStamp = Guid.NewGuid().ToString();
                changed = true;
            }

            user.LockoutEnabled = true;

            if (changed)
            {
                var result = await _userManager.UpdateAsync(user);
                EnsureSuccess(result, $"migrate user {user.Id}");
                _logger.LogInformation("Migrated authentication data for user {UserId}.", user.Id);
            }
        }
    }

    private HashSet<string> GetConfiguredAdminEmails()
    {
        var emails = _configuration.GetSection("Authorization:AdminEmails").Get<string[]>() ?? [];
        var normalizedEmails = emails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => _userManager.NormalizeEmail(email.Trim()))
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (normalizedEmails.Count == 0)
            throw new InvalidOperationException("Authorization:AdminEmails must contain at least one admin email.");

        return normalizedEmails;
    }

    private bool IsConfiguredAdmin(ApplicationUser user, HashSet<string> normalizedAdminEmails)
    {
        var normalizedEmail = _userManager.NormalizeEmail(user.Email);
        return !string.IsNullOrWhiteSpace(normalizedEmail) && normalizedAdminEmails.Contains(normalizedEmail);
    }

    private static void EnsureSuccess(IdentityResult result, string operation)
    {
        if (result.Succeeded)
            return;

        var errors = string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
        throw new InvalidOperationException($"Identity initialization failed while attempting to {operation}: {errors}");
    }
}
