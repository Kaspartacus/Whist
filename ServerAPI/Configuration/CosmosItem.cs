using System.Text.Json.Serialization;

namespace ServerAPI.Configuration;

public sealed class CosmosItem<T>
{
    public CosmosItem()
    {
        Id = "";
        Data = default!;
    }

    public CosmosItem(string id, T data)
    {
        Id = id;
        Data = data;
    }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("data")]
    public T Data { get; set; }
}
