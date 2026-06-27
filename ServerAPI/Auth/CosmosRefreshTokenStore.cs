using ServerAPI.Configuration;

namespace ServerAPI.Auth;

public sealed class CosmosRefreshTokenStore : IRefreshTokenStore
{
    private readonly CosmosDbContext _cosmos;

    public CosmosRefreshTokenStore(CosmosDbContext cosmos)
        => _cosmos = cosmos;

    public async Task StoreAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
        => await CosmosDbContext.UpsertAsync(_cosmos.RefreshTokens, refreshToken.Id, refreshToken, cancellationToken);

    public async Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => (await CosmosDbContext.ReadAllAsync<RefreshToken>(_cosmos.RefreshTokens, cancellationToken))
            .FirstOrDefault(token => token.TokenHash == tokenHash);

    public async Task RevokeAsync(
        string tokenHash,
        string? replacedByTokenHash = null,
        CancellationToken cancellationToken = default)
    {
        var token = await FindByHashAsync(tokenHash, cancellationToken);
        if (token is null || token.RevokedAtUtc is not null)
            return;

        token.RevokedAtUtc = DateTime.UtcNow;
        token.ReplacedByTokenHash = replacedByTokenHash;
        await CosmosDbContext.UpsertAsync(_cosmos.RefreshTokens, token.Id, token, cancellationToken);
    }

    public async Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var tokens = (await CosmosDbContext.ReadAllAsync<RefreshToken>(_cosmos.RefreshTokens, cancellationToken))
            .Where(token => token.UserId == userId && token.RevokedAtUtc is null)
            .ToList();

        foreach (var token in tokens)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
            await CosmosDbContext.UpsertAsync(_cosmos.RefreshTokens, token.Id, token, cancellationToken);
        }
    }
}
