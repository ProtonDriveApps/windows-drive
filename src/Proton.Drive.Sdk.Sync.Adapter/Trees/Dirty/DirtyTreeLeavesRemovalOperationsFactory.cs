using Proton.Drive.Sdk.Sync.Shared.Trees;

namespace Proton.Drive.Sdk.Sync.Adapter.Trees.Dirty;

internal class DirtyTreeLeavesRemovalOperationsFactory<TId> :
    LeavesRemovalOperationsFactory<DirtyTree<TId>, DirtyTreeNode<TId>, DirtyTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    public DirtyTreeLeavesRemovalOperationsFactory()
        : base(model => !model.IsCandidateForDirtyTree())
    { }
}
