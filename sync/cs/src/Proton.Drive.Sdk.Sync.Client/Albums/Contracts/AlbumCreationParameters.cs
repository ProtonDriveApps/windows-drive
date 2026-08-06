using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Albums.Contracts;

public sealed record AlbumCreationParameters
{
    /// <summary>
    /// Indicates if biometrics is required to access this album.
    /// </summary>
    [JsonPropertyName("Locked")]
    public bool BiometricsRequired { get; init; }

    [JsonPropertyName("Link")]
    public required AlbumLinkCreationParameters LinkCreationParameters { get; init; }
}
