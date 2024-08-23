using System.Text.Json.Serialization;

namespace ProtonDrive.App.Settings;

public class RemoteToLocalMapping
{
    /// <summary>
    /// Automatically generated unique identity value.
    /// </summary>
    public int Id { get; set; }

    public MappingType Type { get; set; }

    public SyncMethod SyncMethod { get; set; } = SyncMethod.Classic;

    public MappingStatus Status { get; set; } = MappingStatus.New;

    [JsonIgnore]
    public bool HasSetupSucceeded { get; set; }

    public RemoteReplica Remote { get; set; } = new();

    public LocalReplica Local { get; set; } = new();
}
