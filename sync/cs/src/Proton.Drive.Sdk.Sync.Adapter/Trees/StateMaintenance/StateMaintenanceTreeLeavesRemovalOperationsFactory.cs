using Proton.Drive.Sdk.Sync.Shared.Trees;

namespace Proton.Drive.Sdk.Sync.Adapter.Trees.StateMaintenance;

internal class StateMaintenanceTreeLeavesRemovalOperationsFactory<TId> :
    LeavesRemovalOperationsFactory<StateMaintenanceTree<TId>, StateMaintenanceTreeNode<TId>, StateMaintenanceTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    public StateMaintenanceTreeLeavesRemovalOperationsFactory()
        : base(model => !model.IsCandidateForSyncStateUpdate())
    { }
}
