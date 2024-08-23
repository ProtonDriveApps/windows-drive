using System;
using ProtonDrive.Shared;

namespace ProtonDrive.Client.Shares.SharedWithMe;

public sealed record SharedWithMeItem : IIdentifiable<string>
{
    /// <summary>
    /// Remote share ID.
    /// </summary>
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string LinkId { get; init; }
    public required string VolumeId { get; init; }
    public required bool IsFolder { get; init; }
    public string? InviterEmailAddress { get; init; }
    public required DateTime SharingTime { get; init; }
    public bool IsReadOnly { get; init; }
    public string? MemberId { get; init; }
}
