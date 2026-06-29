using System.Text.Json.Serialization;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record ActivityResponse : ApiResponse
{
    [JsonPropertyName("Active")]
    public bool IsActive { get; init; }
}
