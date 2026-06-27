using System.Security.Claims;
using System.Text.Json;
using Core;
using Microsoft.AspNetCore.Components.Authorization;

namespace WebApp.Service.AuthServices;

public sealed class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly TokenStorage _tokenStorage;
    private bool _legacyDataRemoved;

    public JwtAuthenticationStateProvider(TokenStorage tokenStorage) => _tokenStorage = tokenStorage;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_legacyDataRemoved)
        {
            await _tokenStorage.RemoveLegacyAuthenticationDataAsync();
            _legacyDataRemoved = true;
        }

        var accessToken = await _tokenStorage.GetAccessTokenAsync();
        var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
        var principal = CreatePrincipal(accessToken, allowExpiredToken: !string.IsNullOrWhiteSpace(refreshToken));

        if (principal.Identity?.IsAuthenticated != true)
        {
            if (!string.IsNullOrWhiteSpace(accessToken) && string.IsNullOrWhiteSpace(refreshToken))
                await _tokenStorage.RemoveTokensAsync();
            return Anonymous;
        }

        return new AuthenticationState(principal);
    }

    public async Task<AuthenticatedUser?> GetAuthenticatedUserAsync()
    {
        var state = await GetAuthenticationStateAsync();
        var principal = state.User;
        if (principal.Identity?.IsAuthenticated != true)
            return null;

        var idValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idValue, out var id))
            return null;

        return new AuthenticatedUser
        {
            Id = id,
            Name = principal.Identity.Name ?? "",
            Email = principal.FindFirst(ClaimTypes.Email)?.Value ?? "",
            Roles = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray()
        };
    }

    public void NotifyAuthenticationChanged()
        => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private static ClaimsPrincipal CreatePrincipal(string? token, bool allowExpiredToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Anonymous.User;

        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return Anonymous.User;

            using var payload = JsonDocument.Parse(DecodeBase64Url(parts[1]));
            if (!payload.RootElement.TryGetProperty("exp", out var expElement) ||
                !expElement.TryGetInt64(out var expiry))
            {
                return Anonymous.User;
            }

            if (!allowExpiredToken && DateTimeOffset.FromUnixTimeSeconds(expiry) <= DateTimeOffset.UtcNow)
            {
                return Anonymous.User;
            }

            if (!payload.RootElement.TryGetProperty("sst", out var securityStampElement) ||
                securityStampElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(securityStampElement.GetString()))
            {
                return Anonymous.User;
            }

            var claims = new List<Claim>();
            AddClaim(payload.RootElement, "sub", ClaimTypes.NameIdentifier, claims);
            AddClaim(payload.RootElement, "name", ClaimTypes.Name, claims);
            AddClaim(payload.RootElement, "email", ClaimTypes.Email, claims);
            AddClaims(payload.RootElement, "role", ClaimTypes.Role, claims);

            return new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role));
        }
        catch
        {
            return Anonymous.User;
        }
    }

    private static void AddClaim(JsonElement payload, string propertyName, string claimType, ICollection<Claim> claims)
    {
        if (payload.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
            claims.Add(new Claim(claimType, value.GetString() ?? ""));
    }

    private static void AddClaims(JsonElement payload, string propertyName, string claimType, ICollection<Claim> claims)
    {
        if (!payload.TryGetProperty(propertyName, out var value))
            return;

        if (value.ValueKind == JsonValueKind.String)
        {
            claims.Add(new Claim(claimType, value.GetString() ?? ""));
            return;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    claims.Add(new Claim(claimType, item.GetString() ?? ""));
            }
        }
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => ""
        };
        return Convert.FromBase64String(padded);
    }
}
