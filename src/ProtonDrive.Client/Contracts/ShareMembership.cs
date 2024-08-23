using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public enum MemberState
{
    Active = 1,
    Locked = 3,
}

public sealed record ShareMembership
{
    [JsonPropertyName("MemberID")]
    public string? MemberId { get; init; }

    [JsonPropertyName("ShareID")]
    public string? ShareId { get; init; }

    [JsonPropertyName("AddressID")]
    public string AddressId { get; init; } = string.Empty;

    [JsonPropertyName("Inviter")]
    public string? InviterEmailAddress { get; init; }

    [JsonPropertyName("CreateTime")]
    public long CreationTime { get; init; }
    public MemberPermissions Permissions { get; init; }
    public MemberState State { get; init; }

    public string KeyPacket { get; init; } = string.Empty;

    [JsonPropertyName("Unlockable")]
    public bool? IsUnlockable { get; init; }
}
