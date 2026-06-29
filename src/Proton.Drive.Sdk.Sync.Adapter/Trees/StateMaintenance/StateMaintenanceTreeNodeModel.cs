using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Adapter.Trees.StateMaintenance;

public class StateMaintenanceTreeNodeModel<TId> : FileSystemNodeModel<TId>
    where TId : IEquatable<TId>
{
    public AdapterNodeStatus Status { get; set; }

    public override void CopyMetadataFrom(IFileSystemNodeModel<TId> other)
    {
        Status = other switch
        {
            StateMaintenanceTreeNodeModel<TId> item => item.Status,
            _ => Status,
        };
    }
}
