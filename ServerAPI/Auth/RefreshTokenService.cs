using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace ServerAPI.Auth;

public sealed class RefreshTokenService
{
    private readonly JwtOptions _options;
    private readonly IRefreshTokenStore _refreshTokenStore;

    public RefreshTokenService(IOptions<JwtOptions> options, IRefreshTokenStore refreshTokenStore)
    {
        _options = options.Value;
        _refreshTokenStore = refreshTokenStore;
    }

    public async Task<(string RawToken, DateTime ExpiresAtUtc, string TokenHash)> CreateAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var rawToken = CreateSecureToken();
        var tokenHash = Hash(rawToken);
        var expiresAtUtc = DateTime.UtcNow.AddDays(_options.RefreshTokenDays);

        await _refreshTokenStore.StoreAsync(new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresAtUtc
        }, cancellationToken);

        return (rawToken, expiresAtUtc, tokenHash);
    }

    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(bytes);
    }

    private static string CreateSecureToken()
        => Base64UrlEncode(RandomNumberGenerator.GetBytes(64));

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
