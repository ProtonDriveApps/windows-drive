using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Adapter.Trees.Dirty;

internal class DirtyTreeNode<TId> : FileSystemNode<DirtyTree<TId>, DirtyTreeNode<TId>, DirtyTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    protected internal DirtyTreeNode(
        DirtyTree<TId> tree,
        DirtyTreeNodeModel<TId> model,
        DirtyTreeNode<TId>? parent)
        : base(tree, model, parent)
    {
    }
}
