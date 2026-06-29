using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Albums.Contracts;

public sealed record AlbumResponse
{
    public required bool Locked { get; init; }

    public required long LastActivityTime { get; init; }

    public required int PhotoCount { get; init; }

    [JsonPropertyName("LinkID")]
    public required string LinkId { get; init; }

    [JsonPropertyName("VolumeID")]
    public required string VolumeId { get; init; }

    [JsonPropertyName("ShareID")]
    public string? ShareId { get; init; }
}
