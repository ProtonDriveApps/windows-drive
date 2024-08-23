using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record FolderChildrenDeletionResponse
{
    [JsonPropertyName("LinkID")]
    public string LinkId { get; init; } = string.Empty;

    public ApiResponse Response { get; init; } = new();
}
