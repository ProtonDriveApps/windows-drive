using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Albums.Contracts;

public sealed class LinkIdListParameter
{
    [JsonPropertyName("LinkIDs")]
    public List<string> LinkIds { get; init; } = [];
}
