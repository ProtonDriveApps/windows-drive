using System;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.DataAccess.Databases;

public sealed class FlattenedAdapterTreeNodeModel<TId, TAltId> : FileSystemNodeModel<TId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    public int? VolumeId { get; set; }
    public TAltId? AltId { get; set; }

    public string? RevisionId { get; set; }
    public DateTime LastWriteTime { get; set; }
    public long Size { get; set; }
    public AdapterNodeStatus Status { get; set; }
}
