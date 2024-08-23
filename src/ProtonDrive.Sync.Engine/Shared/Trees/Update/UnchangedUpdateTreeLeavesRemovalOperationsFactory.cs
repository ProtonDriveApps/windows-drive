using System;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.Sync.Engine.Shared.Trees.Update;

internal class UnchangedUpdateTreeLeavesRemovalOperationsFactory<TId> :
    LeavesRemovalOperationsFactory<UpdateTree<TId>, UpdateTreeNode<TId>, UpdateTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    public UnchangedUpdateTreeLeavesRemovalOperationsFactory()
        : base(m => m.Status == UpdateStatus.Unchanged)
    { }
}
