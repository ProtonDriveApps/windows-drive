using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record FolderId
{
    [JsonPropertyName("ID")]
    public string Value { get; init; } = string.Empty;
}
