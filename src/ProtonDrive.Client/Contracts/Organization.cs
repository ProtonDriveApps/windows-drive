using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed class Organization
{
    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("PlanName")]
    public string PlanCode { get; set; } = string.Empty;
}
