using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Albums.Contracts;

public sealed record AlbumDto
{
    [JsonPropertyName("Locked")]
    public required bool BiometricsRequired { get; init; }
    public required long LastActivityTime { get; init; }
    public required int PhotoCount { get; init; }
    public required string NodeHashKey { get; init; }
}
