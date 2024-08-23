using System;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.Collections;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.Trees.StateMaintenance;

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
