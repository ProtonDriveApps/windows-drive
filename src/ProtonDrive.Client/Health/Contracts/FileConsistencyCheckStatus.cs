using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Health.Contracts;

internal sealed class FileConsistencyCheckStatus
{
    [JsonPropertyName("ClientUID")]
    public required string ClientInstanceId { get; init; }

    public int InspectedItemCount { get; init; }
    public int RefreshedItemCount { get; init; }
    public int FailedItemCount { get; init; }

    public required FileConsistencyCheckState State { get; init; }
}
