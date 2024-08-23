using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public enum VolumeState
{
    None = 0,
    Active = 1,
    Deleted = 2,
    Locked = 3,
    Restored = 4,
}

public sealed class Volume
{
    [JsonPropertyName("VolumeID")]
    public required string Id { get; set; }
    public long UsedSpace { get; set; }
    public VolumeState State { get; set; }

    public required VolumeRootShare Share { get; set; }
}
