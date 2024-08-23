using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record SharedWithMeItem
{
    [JsonPropertyName("VolumeID")]
    public required string VolumeId { get; init; }

    [JsonPropertyName("ShareID")]
    public required string ShareId { get; init; }

    [JsonPropertyName("LinkID")]
    public required string LinkId { get; init; }
}
