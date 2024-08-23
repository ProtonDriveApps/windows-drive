using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record ActivityResponse : ApiResponse
{
    [JsonPropertyName("Active")]
    public bool IsActive { get; init; }
}
