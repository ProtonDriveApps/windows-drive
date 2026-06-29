using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Features.Contracts;

public sealed record FeatureFlag
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }
}
