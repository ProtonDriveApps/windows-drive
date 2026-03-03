using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Health.Contracts;

internal sealed record FileConsistencyCheckResponse : ApiResponse
{
    [JsonPropertyName("Check")]
    public bool IsRequired { get; init; }
}
