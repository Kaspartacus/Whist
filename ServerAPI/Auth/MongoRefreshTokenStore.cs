using MongoDB.Driver;

namespace ServerAPI.Auth;

public sealed class MongoRefreshTokenStore : IRefreshTokenStore
{
    private readonly IMongoCollection<RefreshToken> _refreshTokens;

    public MongoRefreshTokenStore(MongoIdentityContext context)
        => _refreshTokens = context.RefreshTokens;

    public async Task StoreAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
        => await _refreshTokens.InsertOneAsync(refreshToken, cancellationToken: cancellationToken);

    public async Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => await _refreshTokens.Find(token => token.TokenHash == tokenHash).FirstOrDefaultAsync(cancellationToken);

    public async Task RevokeAsync(
        string tokenHash,
        string? replacedByTokenHash = null,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<RefreshToken>.Update
            .Set(token => token.RevokedAtUtc, DateTime.UtcNow)
            .Set(token => token.ReplacedByTokenHash, replacedByTokenHash);

        await _refreshTokens.UpdateOneAsync(
            token => token.TokenHash == tokenHash && token.RevokedAtUtc == null,
            update,
            cancellationToken: cancellationToken);
    }

    public async Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var update = Builders<RefreshToken>.Update.Set(token => token.RevokedAtUtc, DateTime.UtcNow);

        await _refreshTokens.UpdateManyAsync(
            token => token.UserId == userId && token.RevokedAtUtc == null,
            update,
            cancellationToken: cancellationToken);
    }
}
