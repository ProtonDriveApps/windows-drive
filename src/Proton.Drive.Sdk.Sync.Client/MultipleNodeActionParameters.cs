using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client;

public sealed class MultipleNodeActionParameters
{
    public MultipleNodeActionParameters(params string[] linkIds)
    {
        LinkIds = linkIds;
    }

    [JsonPropertyName("LinkIDs")]
    public IEnumerable<string> LinkIds { get; }
}
