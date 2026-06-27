using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using ServerAPI.Configuration;

namespace ServerAPI.Auth;

public sealed class MongoIdentityContext
{
    public IMongoCollection<ApplicationUser> Users { get; }
    public IMongoCollection<ApplicationRole> Roles { get; }
    public IMongoCollection<RefreshToken> RefreshTokens { get; }

    public MongoIdentityContext(IConfiguration configuration)
    {
        RegisterMappings();

        var database = MongoDatabaseFactory.Create(configuration);
        Users = database.GetCollection<ApplicationUser>("users");
        Roles = database.GetCollection<ApplicationRole>("identityRoles");
        RefreshTokens = database.GetCollection<RefreshToken>("refreshTokens");

        EnsureIndexes();
    }

    private static void RegisterMappings()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(ApplicationUser)))
        {
            BsonClassMap.RegisterClassMap<ApplicationUser>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
            });
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(ApplicationRole)))
        {
            BsonClassMap.RegisterClassMap<ApplicationRole>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
            });
        }
    }

    private void EnsureIndexes()
    {
        Users.Indexes.CreateOne(new CreateIndexModel<ApplicationUser>(
            Builders<ApplicationUser>.IndexKeys.Ascending(user => user.NormalizedEmail),
            new CreateIndexOptions { Unique = true, Sparse = true }));

        Users.Indexes.CreateOne(new CreateIndexModel<ApplicationUser>(
            Builders<ApplicationUser>.IndexKeys.Ascending(user => user.NormalizedUserName),
            new CreateIndexOptions { Unique = true, Sparse = true }));

        Roles.Indexes.CreateOne(new CreateIndexModel<ApplicationRole>(
            Builders<ApplicationRole>.IndexKeys.Ascending(role => role.NormalizedName),
            new CreateIndexOptions { Unique = true, Sparse = true }));

        RefreshTokens.Indexes.CreateOne(new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(token => token.TokenHash),
            new CreateIndexOptions { Unique = true }));

        RefreshTokens.Indexes.CreateOne(new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(token => token.UserId)));

        RefreshTokens.Indexes.CreateOne(new CreateIndexModel<RefreshToken>(
            Builders<RefreshToken>.IndexKeys.Ascending(token => token.ExpiresAtUtc),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }));
    }
}
