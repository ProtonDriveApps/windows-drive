using System.Text.Json.Serialization;
using Proton.Drive.Sdk.Sync.Agent.Mapping;

namespace Proton.Drive.Sdk.Sync.Agent.Settings;

public sealed class StorageOptimizationState
{
    public bool IsEnabled { get; set; }

    public StorageOptimizationStatus Status { get; set; }

    [JsonIgnore]
    public StorageOptimizationErrorCode ErrorCode { get; set; }

    [JsonIgnore]
    public string? ConflictingProviderName { get; set; }
}
