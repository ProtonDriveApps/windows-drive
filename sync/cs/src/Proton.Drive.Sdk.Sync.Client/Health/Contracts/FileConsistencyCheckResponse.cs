using System.Text.Json.Serialization;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Health.Contracts;

internal sealed record FileConsistencyCheckResponse : ApiResponse
{
    [JsonPropertyName("Check")]
    public bool IsRequired { get; init; }
}
