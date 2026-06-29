namespace ServerAPI.Storage;

public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    public string ConnectionString { get; set; } = "";
    public string ContainerName { get; set; } = "images";
    public string? PublicBaseUrl { get; set; }
    public int MaxImageWidth { get; set; } = 1600;
    public int WebpQuality { get; set; } = 82;
    public long MaxUploadBytes { get; set; } = 5 * 1024 * 1024;
}
