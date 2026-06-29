using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Albums.Contracts;

public sealed record PhotoToAddListParameters
{
    [JsonPropertyName("AlbumData")]
    public IReadOnlyCollection<PhotoToAddParameter> Photos { get; init; } = [];
}
