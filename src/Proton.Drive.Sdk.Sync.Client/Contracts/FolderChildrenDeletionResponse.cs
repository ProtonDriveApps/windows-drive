using System.Text.Json.Serialization;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record FolderChildrenDeletionResponse
{
    [JsonPropertyName("LinkID")]
    public string LinkId { get; init; } = string.Empty;

    public ApiResponse Response { get; init; } = new();
}
