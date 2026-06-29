using Proton.Drive.Sdk.Sync.Shared.Trees;

namespace Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Update;

internal class UnchangedUpdateTreeLeavesRemovalOperationsFactory<TId> :
    LeavesRemovalOperationsFactory<UpdateTree<TId>, UpdateTreeNode<TId>, UpdateTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    public UnchangedUpdateTreeLeavesRemovalOperationsFactory()
        : base(m => m.Status == UpdateStatus.Unchanged)
    { }
}
