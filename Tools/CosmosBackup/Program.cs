using System.IO.Compression;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Cosmos;

var options = BackupOptions.FromEnvironment();
var startedAt = DateTimeOffset.UtcNow;
var backupPrefix = $"{options.BackupPrefix.Trim('/')}/{startedAt:yyyy-MM-dd/HHmmss}-utc";

Console.WriteLine($"Starting Cosmos backup for database '{options.CosmosDatabaseName}'.");
Console.WriteLine($"Backup destination: container '{options.BackupContainerName}', prefix '{backupPrefix}'.");
Console.WriteLine($"Containers: {string.Join(", ", options.ContainerNames)}");

var cosmosClient = new CosmosClient(options.CosmosConnectionString, new CosmosClientOptions
{
    ConnectionMode = ConnectionMode.Gateway,
    RequestTimeout = TimeSpan.FromSeconds(30)
});

var database = cosmosClient.GetDatabase(options.CosmosDatabaseName);
var backupContainer = new BlobContainerClient(options.BackupStorageConnectionString, options.BackupContainerName);
await backupContainer.CreateIfNotExistsAsync(PublicAccessType.None);

var results = new List<ContainerBackupResult>();

foreach (var containerName in options.ContainerNames)
{
    var result = await BackupContainerAsync(database, backupContainer, backupPrefix, containerName);
    results.Add(result);

    Console.WriteLine(
        $"Backed up '{containerName}': {result.DocumentCount} documents, {result.CompressedBytes} compressed bytes.");
}

var completedAt = DateTimeOffset.UtcNow;
var manifest = new BackupManifest(
    StartedAtUtc: startedAt,
    CompletedAtUtc: completedAt,
    CosmosDatabaseName: options.CosmosDatabaseName,
    BackupPrefix: backupPrefix,
    Containers: results);

await UploadJsonAsync(
    backupContainer,
    $"{backupPrefix}/manifest.json",
    manifest,
    "application/json");

if (options.Uploads is not null)
    await BackupUploadsAsync(options.Uploads, backupContainer, backupPrefix);
else
    Console.WriteLine("Upload blob backup skipped because upload storage configuration is not set.");

Console.WriteLine($"Backup completed successfully in {(completedAt - startedAt).TotalSeconds:N1} seconds.");

static async Task<ContainerBackupResult> BackupContainerAsync(
    Database database,
    BlobContainerClient backupContainer,
    string backupPrefix,
    string containerName)
{
    var container = database.GetContainer(containerName);
    await using var output = new MemoryStream();
    var documentCount = 0;

    await using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
    await using (var writer = new Utf8JsonWriter(gzip, new JsonWriterOptions { Indented = false }))
    {
        writer.WriteStartArray();

        var iterator = container.GetItemQueryStreamIterator(
            new QueryDefinition("SELECT * FROM c"),
            requestOptions: new QueryRequestOptions
            {
                MaxItemCount = 100
            });

        while (iterator.HasMoreResults)
        {
            using var response = await iterator.ReadNextAsync();
            response.EnsureSuccessStatusCode();

            using var page = await JsonDocument.ParseAsync(response.Content);
            if (!page.RootElement.TryGetProperty("Documents", out var documents))
                throw new InvalidOperationException($"Cosmos response for container '{containerName}' did not contain Documents.");

            foreach (var document in documents.EnumerateArray())
            {
                document.WriteTo(writer);
                documentCount++;
            }
        }

        writer.WriteEndArray();
        await writer.FlushAsync();
    }

    output.Position = 0;
    var blobName = $"{backupPrefix}/{containerName}.json.gz";
    var blob = backupContainer.GetBlobClient(blobName);

    await blob.UploadAsync(output, new BlobUploadOptions
    {
        HttpHeaders = new BlobHttpHeaders
        {
            ContentType = "application/json",
            ContentEncoding = "gzip"
        },
        Metadata = new Dictionary<string, string>
        {
            ["cosmosDatabase"] = database.Id,
            ["cosmosContainer"] = containerName,
            ["documentCount"] = documentCount.ToString()
        }
    });

    return new ContainerBackupResult(
        ContainerName: containerName,
        BlobName: blobName,
        DocumentCount: documentCount,
        CompressedBytes: output.Length);
}

static async Task UploadJsonAsync<T>(
    BlobContainerClient backupContainer,
    string blobName,
    T value,
    string contentType)
{
    await using var stream = new MemoryStream();
    await JsonSerializer.SerializeAsync(stream, value, new JsonSerializerOptions
    {
        WriteIndented = true
    });
    stream.Position = 0;

    await backupContainer.GetBlobClient(blobName).UploadAsync(stream, new BlobUploadOptions
    {
        HttpHeaders = new BlobHttpHeaders
        {
            ContentType = contentType
        }
    });
}

static async Task BackupUploadsAsync(
    UploadBackupOptions options,
    BlobContainerClient backupContainer,
    string backupPrefix)
{
    var sourceContainer = new BlobContainerClient(options.ConnectionString, options.ContainerName);
    var destinationPrefix = $"{options.BackupPrefix.Trim('/')}/{backupPrefix}";

    Console.WriteLine($"Starting upload blob backup from container '{options.ContainerName}'.");
    Console.WriteLine($"Upload backup destination prefix: '{destinationPrefix}'.");

    var copied = 0;
    long copiedBytes = 0;

    await foreach (var blobItem in sourceContainer.GetBlobsAsync())
    {
        var sourceBlob = sourceContainer.GetBlobClient(blobItem.Name);
        var destinationBlobName = $"{destinationPrefix}/{blobItem.Name}";
        var destinationBlob = backupContainer.GetBlobClient(destinationBlobName);
        var properties = await sourceBlob.GetPropertiesAsync();

        await using var content = await sourceBlob.OpenReadAsync();
        await destinationBlob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = properties.Value.ContentType,
                CacheControl = properties.Value.CacheControl,
                ContentDisposition = properties.Value.ContentDisposition,
                ContentEncoding = properties.Value.ContentEncoding,
                ContentLanguage = properties.Value.ContentLanguage
            }
        });

        copied++;
        copiedBytes += blobItem.Properties.ContentLength ?? 0;
    }

    var manifest = new UploadBackupManifest(
        SourceContainerName: options.ContainerName,
        BackupPrefix: destinationPrefix,
        BlobCount: copied,
        TotalSourceBytes: copiedBytes);

    await UploadJsonAsync(
        backupContainer,
        $"{destinationPrefix}/manifest.json",
        manifest,
        "application/json");

    Console.WriteLine($"Upload blob backup completed: {copied} blobs, {copiedBytes} source bytes.");
}

internal sealed record BackupOptions(
    string CosmosConnectionString,
    string CosmosDatabaseName,
    string BackupStorageConnectionString,
    string BackupContainerName,
    string BackupPrefix,
    IReadOnlyList<string> ContainerNames,
    UploadBackupOptions? Uploads)
{
    private static readonly string[] DefaultContainerNames =
    [
        "users",
        "identityRoles",
        "fines",
        "highlights",
        "counters",
        "points",
        "calendar",
        "rules"
    ];

    public static BackupOptions FromEnvironment()
    {
        var containerNames = ReadContainerNames();
        return new BackupOptions(
            CosmosConnectionString: Required("COSMOS_CONNECTION_STRING"),
            CosmosDatabaseName: Required("COSMOS_DATABASE_NAME"),
            BackupStorageConnectionString: Required("BACKUP_STORAGE_CONNECTION_STRING"),
            BackupContainerName: Required("BACKUP_CONTAINER_NAME"),
            BackupPrefix: Environment.GetEnvironmentVariable("BACKUP_PREFIX") ?? "cosmos-prod",
            ContainerNames: containerNames,
            Uploads: UploadBackupOptions.FromEnvironment());
    }

    private static IReadOnlyList<string> ReadContainerNames()
    {
        var configured = Environment.GetEnvironmentVariable("BACKUP_CONTAINERS");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var containers = DefaultContainerNames.ToList();
        if (string.Equals(
            Environment.GetEnvironmentVariable("BACKUP_INCLUDE_REFRESH_TOKENS"),
            "true",
            StringComparison.OrdinalIgnoreCase))
        {
            containers.Add("refreshTokens");
        }

        return containers;
    }

    private static string Required(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Required environment variable '{name}' is missing.");

        return value;
    }
}

internal sealed record UploadBackupOptions(
    string ConnectionString,
    string ContainerName,
    string BackupPrefix)
{
    public static UploadBackupOptions? FromEnvironment()
    {
        var connectionString = Environment.GetEnvironmentVariable("UPLOAD_STORAGE_CONNECTION_STRING");
        var containerName = Environment.GetEnvironmentVariable("UPLOAD_CONTAINER_NAME");
        if (string.IsNullOrWhiteSpace(connectionString) && string.IsNullOrWhiteSpace(containerName))
            return null;

        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(containerName))
            throw new InvalidOperationException("UPLOAD_STORAGE_CONNECTION_STRING and UPLOAD_CONTAINER_NAME must both be set to back up upload blobs.");

        return new UploadBackupOptions(
            ConnectionString: connectionString,
            ContainerName: containerName,
            BackupPrefix: Environment.GetEnvironmentVariable("UPLOAD_BACKUP_PREFIX") ?? "uploads");
    }
}

internal sealed record BackupManifest(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string CosmosDatabaseName,
    string BackupPrefix,
    IReadOnlyList<ContainerBackupResult> Containers);

internal sealed record ContainerBackupResult(
    string ContainerName,
    string BlobName,
    int DocumentCount,
    long CompressedBytes);

internal sealed record UploadBackupManifest(
    string SourceContainerName,
    string BackupPrefix,
    int BlobCount,
    long TotalSourceBytes);
