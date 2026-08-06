using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record RevisionId
{
    [JsonPropertyName("ID")]
    public string Value { get; init; } = string.Empty;
}
