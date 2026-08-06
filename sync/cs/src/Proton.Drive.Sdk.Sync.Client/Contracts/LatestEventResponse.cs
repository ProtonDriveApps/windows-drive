using System.Text.Json.Serialization;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record LatestEventResponse : ApiResponse
{
    [JsonPropertyName("EventID")]
    public string? AnchorId { get; init; }
}
