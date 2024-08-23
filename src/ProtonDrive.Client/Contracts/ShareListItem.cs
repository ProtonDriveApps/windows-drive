using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public enum ShareType
{
    Main = 1,
    Standard = 2,
    Device = 3,
    Photos = 4,
}

public enum ShareFlag
{
    None = 0,
    Primary = 1,
}

public enum ShareState
{
    Active = 1,
    Deleted = 2,
    Restored = 3,
}

public record ShareListItem
{
    [JsonPropertyName("ShareID")]
    public string Id { get; init; } = string.Empty;

    public ShareType Type { get; init; }

    public ShareState State { get; init; }

    [JsonPropertyName("LinkID")]
    public string LinkId { get; init; } = string.Empty;

    [JsonPropertyName("VolumeID")]
    public string VolumeId { get; init; } = string.Empty;

    [JsonPropertyName("Creator")]
    public string? CreatorEmailAddress { get; init; }

    public ShareFlag Flags { get; init; }

    [JsonPropertyName("Locked")]
    public bool IsLocked { get; init; }

    [JsonPropertyName("VolumeSoftDeleted")]
    public bool IsVolumeSoftDeleted { get; init; }
}
