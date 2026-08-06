using System.Text.Json.Serialization;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Core.Events.Contracts;

internal sealed record CoreLatestEventResponse : ApiResponse
{
    [JsonPropertyName("EventID")]
    public string? AnchorId { get; init; }
}
