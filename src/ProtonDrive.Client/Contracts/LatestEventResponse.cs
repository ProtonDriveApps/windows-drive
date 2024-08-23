using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record LatestEventResponse : ApiResponse
{
    [JsonPropertyName("EventID")]
    public string? AnchorId { get; init; }
}
