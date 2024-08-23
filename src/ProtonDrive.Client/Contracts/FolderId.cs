using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record FolderId
{
    [JsonPropertyName("ID")]
    public string Value { get; init; } = string.Empty;
}
