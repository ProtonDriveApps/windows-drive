using System;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.Trees.Dirty;

public class DirtyTreeNodeModel<TId> : FileSystemNodeModel<TId>
    where TId : IEquatable<TId>
{
    public AdapterNodeStatus Status { get; set; }

    public override void CopyMetadataFrom(IFileSystemNodeModel<TId> other)
    {
        Status = other switch
        {
            DirtyTreeNodeModel<TId> item => item.Status,
            _ => Status
        };
    }
}
