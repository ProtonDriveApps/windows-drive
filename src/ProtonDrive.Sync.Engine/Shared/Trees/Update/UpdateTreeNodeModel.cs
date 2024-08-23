using System;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.Shared.Trees.Update;

public class UpdateTreeNodeModel<TId> : FileSystemNodeModel<TId>
    where TId : IEquatable<TId>
{
    public UpdateStatus Status { get; set; }

    public override void CopyMetadataFrom(IFileSystemNodeModel<TId> other)
    {
        if (other is UpdateTreeNodeModel<TId> item)
        {
            Status = item.Status;
        }
    }
}
