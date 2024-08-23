using System;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.Trees.StateMaintenance;

internal class StateMaintenanceTreeNodeFactory<TId> : IFileSystemNodeFactory<StateMaintenanceTree<TId>, StateMaintenanceTreeNode<TId>, StateMaintenanceTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    public StateMaintenanceTreeNode<TId> CreateRootNode(StateMaintenanceTree<TId> tree)
    {
        return CreateNode(tree, new StateMaintenanceTreeNodeModel<TId>(), default);
    }

    public StateMaintenanceTreeNode<TId> CreateNode(
        StateMaintenanceTree<TId> tree,
        StateMaintenanceTreeNodeModel<TId> model,
        StateMaintenanceTreeNode<TId>? parent)
    {
        return new StateMaintenanceTreeNode<TId>(tree, model, parent);
    }
}
