using System.Text.Json.Serialization;

namespace ServerAPI.Auth;

public sealed class RefreshToken
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public int UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    [JsonIgnore]
    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}
