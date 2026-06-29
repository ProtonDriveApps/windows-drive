using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Albums.Contracts;

public sealed class AlbumProperties
{
    /// <summary>
    /// Indicates if biometrics is required to access this album.
    /// </summary>
    [JsonPropertyName("Locked")]
    public bool BiometricsRequired { get; init; }

    public required string NodeHashKey { get; init; }

    public required long LastActivityTime { get; init; }

    public required int PhotoCount { get; init; }

    [JsonPropertyName("CoverLinkID")]
    public string? CoverLinkId { get; init; }
}
