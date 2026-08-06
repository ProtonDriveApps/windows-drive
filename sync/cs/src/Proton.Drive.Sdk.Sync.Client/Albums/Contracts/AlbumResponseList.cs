using System.Text.Json.Serialization;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Albums.Contracts;

public sealed record AlbumResponseList : ApiResponse
{
    public IReadOnlyCollection<AlbumResponse> Albums { get; init; } = [];

    [JsonPropertyName("AnchorID")]
    public string? AnchorId { get; init; }

    [JsonPropertyName("More")]
    public required bool HasMoreData { get; init; }
}
