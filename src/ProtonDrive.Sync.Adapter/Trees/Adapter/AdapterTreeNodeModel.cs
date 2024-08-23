using System;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.Trees.Adapter;

public class AdapterTreeNodeModel<TId, TAltId> : LooseCompoundAltIdentifiableFileSystemNodeModel<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    public string? RevisionId { get; set; }
    public DateTime LastWriteTime { get; set; }
    public long Size { get; set; }
    public AdapterNodeStatus Status { get; set; }

    public override void CopyMetadataFrom(IFileSystemNodeModel<TId> other)
    {
        base.CopyMetadataFrom(other);

        if (other is AdapterTreeNodeModel<TId, TAltId> item)
        {
            RevisionId = item.RevisionId;
            LastWriteTime = item.LastWriteTime;
            Size = item.Size;
            Status = item.Status;
        }
    }
}
