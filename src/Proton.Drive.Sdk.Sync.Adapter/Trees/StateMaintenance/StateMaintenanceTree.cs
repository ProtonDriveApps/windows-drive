using Proton.Drive.Sdk.Sync.Shared.Trees;
using Proton.Drive.Sdk.Sync.Shared.Trees.Collections;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Adapter.Trees.StateMaintenance;

internal class StateMaintenanceTree<TId> : FileSystemTree<StateMaintenanceTree<TId>, StateMaintenanceTreeNode<TId>, StateMaintenanceTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    public StateMaintenanceTree(
        ITreeNodeRepository<StateMaintenanceTreeNodeModel<TId>, TId> repository,
        IFileSystemNodeFactory<StateMaintenanceTree<TId>, StateMaintenanceTreeNode<TId>, StateMaintenanceTreeNodeModel<TId>, TId> factory)
        : base(repository, factory, new IdentifiableNodeDictionary<StateMaintenanceTreeNode<TId>, StateMaintenanceTreeNodeModel<TId>, TId>())
    {
    }
}
