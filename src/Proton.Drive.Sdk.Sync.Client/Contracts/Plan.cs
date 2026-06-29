using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record Plan
{
    public required PlanType Type { get; init; }

    [JsonPropertyName("Name")]
    public required string? Code { get; init; }

    [JsonPropertyName("Title")]
    public string? DisplayName { get; init; }
}
