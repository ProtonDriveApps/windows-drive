using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Volumes.Contracts;

public enum VolumeState
{
    Active = 1,
    Locked = 3,
}

public enum VolumeType
{
    Main = 1,
    Photo = 2,
}

public sealed class Volume
{
    [JsonPropertyName("VolumeID")]
    public required string Id { get; set; }
    public long UsedSpace { get; set; }
    public VolumeState State { get; set; }
    public VolumeType Type { get; set; }

    public required VolumeRootShare Share { get; set; }
}
