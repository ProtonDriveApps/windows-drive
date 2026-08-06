using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Adapter.Trees.Dirty;

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
