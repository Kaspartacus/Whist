namespace ServerAPI.Auth;

public interface IRefreshTokenStore
{
    Task StoreAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task RevokeAsync(string tokenHash, string? replacedByTokenHash = null, CancellationToken cancellationToken = default);
    Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken = default);
}
