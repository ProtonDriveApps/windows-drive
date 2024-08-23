using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public class EventListItem
{
    [JsonPropertyName("EventID")]
    public required string Id { get; init; }

    [JsonPropertyName("EventType")]
    public EventType Type { get; init; }
    public int CreateTime { get; init; }
    public Link? Link { get; init; }

    [JsonPropertyName("ContextShareID")]
    public string? ContextShareId { get; init; }

    [JsonPropertyName("FromContextShareID")]
    public string? FromContextShareId { get; init; }
}
