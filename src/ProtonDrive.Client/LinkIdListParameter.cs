using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProtonDrive.Client;

public sealed class LinkIdListParameter
{
    [JsonPropertyName("LinkIDs")]
    public List<string> LinkIds { get; } = new();
}
