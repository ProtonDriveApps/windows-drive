using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Albums.Contracts;

public sealed class LinkIdListParameter
{
    [JsonPropertyName("LinkIDs")]
    public List<string> LinkIds { get; init; } = [];
}
