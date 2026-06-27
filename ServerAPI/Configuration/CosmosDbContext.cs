using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace ServerAPI.Configuration;

public sealed class CosmosDbContext
{
    public Container Users { get; }
    public Container Roles { get; }
    public Container RefreshTokens { get; }
    public Container Highlights { get; }
    public Container Counters { get; }
    public Container Points { get; }
    public Container Calendar { get; }
    public Container Rules { get; }

    public CosmosDbContext(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Database is required.");

        var databaseName = configuration["Database:Name"];
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("Database:Name is required.");

        var client = new CosmosClient(connectionString, new CosmosClientOptions
        {
            Serializer = new SystemTextJsonCosmosSerializer()
        });

        var database = client.CreateDatabaseIfNotExistsAsync(databaseName).GetAwaiter().GetResult().Database;

        Users = CreateContainer(database, GetRequiredContainerName(configuration, "Users"));
        Roles = CreateContainer(database, GetRequiredContainerName(configuration, "Roles"));
        RefreshTokens = CreateContainer(database, GetRequiredContainerName(configuration, "RefreshTokens"));
        Highlights = CreateContainer(database, GetRequiredContainerName(configuration, "Highlights"));
        Counters = CreateContainer(database, GetRequiredContainerName(configuration, "Counters"));
        Points = CreateContainer(database, GetRequiredContainerName(configuration, "Points"));
        Calendar = CreateContainer(database, GetRequiredContainerName(configuration, "Calendar"));
        Rules = CreateContainer(database, GetRequiredContainerName(configuration, "Rules"));
    }

    public static async Task<List<T>> ReadAllAsync<T>(Container container, CancellationToken cancellationToken = default)
    {
        var query = container.GetItemLinqQueryable<CosmosItem<T>>().ToFeedIterator();
        var results = new List<T>();

        while (query.HasMoreResults)
        {
            var page = await query.ReadNextAsync(cancellationToken);
            results.AddRange(page.Select(item => item.Data));
        }

        return results;
    }

    public static async Task<T?> ReadByDocumentIdAsync<T>(
        Container container,
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await container.ReadItemAsync<CosmosItem<T>>(
                id,
                new PartitionKey(id),
                cancellationToken: cancellationToken);
            return response.Resource.Data;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return default;
        }
    }

    public static async Task UpsertAsync<T>(
        Container container,
        string id,
        T item,
        CancellationToken cancellationToken = default)
    {
        await container.UpsertItemAsync(
            new CosmosItem<T>(id, item),
            new PartitionKey(id),
            cancellationToken: cancellationToken);
    }

    public static async Task DeleteAsync(
        Container container,
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await container.DeleteItemAsync<CosmosItem<object>>(
                id,
                new PartitionKey(id),
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
        }
    }

    public async Task<int> GetNextIdAsync(string counterName, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await Counters.PatchItemAsync<CosmosItem<CosmosCounter>>(
                counterName,
                new PartitionKey(counterName),
                [PatchOperation.Increment("/data/seq", 1)],
                cancellationToken: cancellationToken);

            return response.Resource.Data.Seq;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            try
            {
                var counter = new CosmosCounter { Name = counterName, Seq = 1 };
                await Counters.CreateItemAsync(
                    new CosmosItem<CosmosCounter>(counterName, counter),
                    new PartitionKey(counterName),
                    cancellationToken: cancellationToken);

                return counter.Seq;
            }
            catch (CosmosException conflict) when (conflict.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                return await GetNextIdAsync(counterName, cancellationToken);
            }
        }
    }

    private static Container CreateContainer(Database database, string name)
    {
        return database
            .CreateContainerIfNotExistsAsync(new ContainerProperties(name, "/id"))
            .GetAwaiter()
            .GetResult()
            .Container;
    }

    private static string GetRequiredContainerName(IConfiguration configuration, string key)
    {
        var value = configuration[$"Database:Containers:{key}"];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Database:Containers:{key} is required.");

        return value;
    }
}
