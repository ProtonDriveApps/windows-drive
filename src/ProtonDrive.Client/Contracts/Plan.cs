using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record Plan
{
    public PlanType Type { get; init; }

    [JsonPropertyName("Name")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("Title")]
    public string DisplayName { get; init; } = string.Empty;
}
