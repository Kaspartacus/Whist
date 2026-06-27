using MongoDB.Driver;

namespace ServerAPI.Configuration;

public static class MongoDatabaseFactory
{
    public static IMongoDatabase Create(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Database is required.");

        var mongoUrl = MongoUrl.Create(connectionString);
        var databaseName = mongoUrl.DatabaseName;
        if (string.IsNullOrWhiteSpace(databaseName))
            databaseName = configuration["Database:Name"];

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("Database name is required. Put it in the Mongo connection string path or set Database:Name.");

        return new MongoClient(connectionString).GetDatabase(databaseName);
    }
}
