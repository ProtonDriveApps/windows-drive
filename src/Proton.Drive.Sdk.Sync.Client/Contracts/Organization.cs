using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed class Organization
{
    public string? Name { get; set; }

    public string? DisplayName { get; set; }

    [JsonPropertyName("PlanName")]
    public string? PlanCode { get; set; }
}
