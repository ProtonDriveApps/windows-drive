using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Albums.Contracts;

public sealed record AlbumResponseList : ApiResponse
{
    public IReadOnlyCollection<AlbumResponse> Albums { get; init; } = [];

    [JsonPropertyName("AnchorID")]
    public string? AnchorId { get; init; }

    [JsonPropertyName("More")]
    public required bool HasMoreData { get; init; }
}
