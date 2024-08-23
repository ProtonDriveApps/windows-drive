using System;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.Shared.Trees.Update;

public class UpdateTreeNode<TId> : FileSystemNode<UpdateTree<TId>, UpdateTreeNode<TId>, UpdateTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    protected internal UpdateTreeNode(
        UpdateTree<TId> tree,
        UpdateTreeNodeModel<TId> model,
        UpdateTreeNode<TId>? parent)
        : base(tree, model, parent)
    {
    }
}
