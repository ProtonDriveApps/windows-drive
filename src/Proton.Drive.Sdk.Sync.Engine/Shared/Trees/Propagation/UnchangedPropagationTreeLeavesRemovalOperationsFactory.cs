using Proton.Drive.Sdk.Sync.Shared.Trees;

namespace Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Propagation;

internal class UnchangedPropagationTreeLeavesRemovalOperationsFactory<TId> :
    LeavesRemovalOperationsFactory<PropagationTree<TId>, PropagationTreeNode<TId>, PropagationTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    public UnchangedPropagationTreeLeavesRemovalOperationsFactory()
        : base(m => m.RemoteStatus == UpdateStatus.Unchanged && m.LocalStatus == UpdateStatus.Unchanged)
    { }
}
