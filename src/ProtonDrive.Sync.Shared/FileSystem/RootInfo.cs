using System;

namespace ProtonDrive.Sync.Shared.FileSystem;

public sealed record RootInfo<TId>(int Id, int VolumeId, TId NodeId) : ICloneable
    where TId : IEquatable<TId>
{
    /// <summary>
    /// The unique identity. Equals to the mapping identity value.
    /// </summary>
    public int Id { get; init; } = Id;

    /// <summary>
    /// The identity of the disk volume.
    /// </summary>
    public int VolumeId { get; init; } = VolumeId;

    /// <summary>
    /// The identity of the root folder on the file system.
    /// </summary>
    public TId NodeId { get; init; } = NodeId;

    /// <summary>
    /// The scope of events.
    /// </summary>
    public string EventScope { get; init; } = string.Empty;

    /// <summary>
    /// If roots have the same <see cref="MoveScope"/> value, moving file
    /// system items from one root to another is supported on the opposite replica.
    /// If roots have different <see cref="MoveScope"/> values, moving from
    /// one root to another is reported to the Sync Engine as creation on the
    /// destination parent and deletion on the source parent.
    /// </summary>
    public int MoveScope { get; init; }

    /// <summary>
    /// Uses on-demand hydration of files.
    /// </summary>
    public bool IsOnDemand { get; init; }

    /// <summary>
    /// Root folder path on the local file system. Used for displaying purposes on the UI
    /// and for opening the local folder.
    /// </summary>
    public string LocalPath { get; init; } = string.Empty;

    /// <summary>
    /// Indicates if the root has been successfully set up and is ready to be synced.
    /// </summary>
    public bool IsEnabled { get; init; }

    object ICloneable.Clone() => MemberwiseClone();

    public bool Equals(RootInfo<TId>? other) => Id.Equals(other?.Id);

    public override int GetHashCode() => Id.GetHashCode();
}
