using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Core.Events.Contracts;

internal sealed record CoreLatestEventResponse : ApiResponse
{
    [JsonPropertyName("EventID")]
    public string? AnchorId { get; init; }
}
