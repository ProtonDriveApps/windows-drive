using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record FileRevisionId
{
    [JsonPropertyName("ID")]
    public string LinkId { get; init; } = string.Empty;

    [JsonPropertyName("RevisionID")]
    public string Value { get; init; } = string.Empty;
}
