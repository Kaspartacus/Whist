using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServerAPI.Auth;

public sealed class RefreshToken
{
    [BsonId]
    public ObjectId Id { get; set; }

    public int UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    [BsonIgnore]
    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}
