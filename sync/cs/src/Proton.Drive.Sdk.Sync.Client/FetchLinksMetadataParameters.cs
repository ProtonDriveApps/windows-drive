using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client;

public sealed class FetchLinksMetadataParameters
{
    [JsonPropertyName("LinkIDs")]
    public required IReadOnlyList<string> LinkIds { get; init; } = [];
}
