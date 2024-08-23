using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record EventListResponse : ApiResponse
{
    private IImmutableList<EventListItem>? _events;

    [JsonPropertyName("EventID")]
    public string? AnchorId { get; init; }

    public IImmutableList<EventListItem> Events
    {
        get => _events ??= ImmutableList<EventListItem>.Empty;
        init => _events = value;
    }

    [JsonPropertyName("More")]
    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    public bool HasMoreData { get; init; }

    [JsonPropertyName("Refresh")]
    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    public bool RequiresRefresh { get; init; }
}
