using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProtonDrive.Client;

public sealed class MultipleNodeActionParameters
{
    public MultipleNodeActionParameters(params string[] linkIds)
    {
        LinkIds = linkIds;
    }

    [JsonPropertyName("LinkIDs")]
    public IEnumerable<string> LinkIds { get; }
}
