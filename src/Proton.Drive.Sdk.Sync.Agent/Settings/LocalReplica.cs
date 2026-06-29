using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Agent.Settings;

public sealed class LocalReplica
{
    public int VolumeSerialNumber { get; set; }

    [JsonPropertyName("RootFolderPath")]
    public string Path { get; set; } = string.Empty;

    public long RootFolderId { get; set; }

    /// <summary>
    /// Automatically generated volume identity for internal use
    /// </summary>
    public int InternalVolumeId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StorageOptimizationState? StorageOptimization { get; set; }
}
