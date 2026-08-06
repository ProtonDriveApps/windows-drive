using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record SharedWithMeItemListResponse : ApiResponse
{
    private IImmutableList<SharedWithMeItem>? _items;

    [JsonPropertyName("Links")]
    public IImmutableList<SharedWithMeItem> Items
    {
        get => _items ??= ImmutableList<SharedWithMeItem>.Empty;
        init => _items = value;
    }

    [JsonPropertyName("AnchorID")]
    public string? AnchorId { get; init; }

    [JsonPropertyName("More")]
    public bool HasMoreData { get; init; }
}
