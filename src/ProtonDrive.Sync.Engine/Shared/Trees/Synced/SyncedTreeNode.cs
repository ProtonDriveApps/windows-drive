using System;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.Shared.Trees.Synced;

public class SyncedTreeNode<TId> : FileSystemNode<SyncedTree<TId>, SyncedTreeNode<TId>, SyncedTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    protected internal SyncedTreeNode(
        SyncedTree<TId> tree,
        SyncedTreeNodeModel<TId> model,
        SyncedTreeNode<TId>? parent)
        : base(tree, model, parent)
    {
    }

    public TId AltId => Model.AltId;
}
