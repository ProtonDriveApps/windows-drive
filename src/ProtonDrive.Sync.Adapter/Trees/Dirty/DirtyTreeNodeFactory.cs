using System;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.Trees.Dirty;

internal class DirtyTreeNodeFactory<TId> : IFileSystemNodeFactory<DirtyTree<TId>, DirtyTreeNode<TId>, DirtyTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    public DirtyTreeNode<TId> CreateRootNode(DirtyTree<TId> tree)
    {
        return CreateNode(tree, new DirtyTreeNodeModel<TId>(), default);
    }

    public DirtyTreeNode<TId> CreateNode(
        DirtyTree<TId> tree,
        DirtyTreeNodeModel<TId> model,
        DirtyTreeNode<TId>? parent)
    {
        return new DirtyTreeNode<TId>(tree, model, parent);
    }
}
