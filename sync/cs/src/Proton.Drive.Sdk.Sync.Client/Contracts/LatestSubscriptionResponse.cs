using System.Text.Json.Serialization;
using Proton.Drive.Shared.Client;
using Proton.Drive.Shared.Text.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record LatestSubscriptionResponse : ApiResponse
{
    [JsonPropertyName("LastSubscriptionEnd")]
    [JsonConverter(typeof(EpochSecondsJsonConverter))]
    public DateTime? CancellationTimeUtc { get; init; }
}
