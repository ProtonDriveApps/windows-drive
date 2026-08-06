using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Albums.Contracts;

public sealed record AddedPhotoWithLinkIdResponse
{
    [JsonPropertyName("LinkID")]
    public required string LinkId { get; init; }

    public required AddedPhotoResponse Response { get; init; }
}
