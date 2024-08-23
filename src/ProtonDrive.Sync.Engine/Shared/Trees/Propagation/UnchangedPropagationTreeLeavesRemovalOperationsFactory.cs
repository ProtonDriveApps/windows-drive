using System;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.Sync.Engine.Shared.Trees.Propagation;

internal class UnchangedPropagationTreeLeavesRemovalOperationsFactory<TId> :
    LeavesRemovalOperationsFactory<PropagationTree<TId>, PropagationTreeNode<TId>, PropagationTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    public UnchangedPropagationTreeLeavesRemovalOperationsFactory()
        : base(m => m.RemoteStatus == UpdateStatus.Unchanged && m.LocalStatus == UpdateStatus.Unchanged)
    { }
}
