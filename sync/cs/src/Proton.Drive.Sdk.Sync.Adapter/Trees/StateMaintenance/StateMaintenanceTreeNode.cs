using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Adapter.Trees.StateMaintenance;

internal class StateMaintenanceTreeNode<TId> : FileSystemNode<StateMaintenanceTree<TId>, StateMaintenanceTreeNode<TId>, StateMaintenanceTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    protected internal StateMaintenanceTreeNode(
        StateMaintenanceTree<TId> tree,
        StateMaintenanceTreeNodeModel<TId> model,
        StateMaintenanceTreeNode<TId>? parent)
        : base(tree, model, parent)
    {
    }
}
