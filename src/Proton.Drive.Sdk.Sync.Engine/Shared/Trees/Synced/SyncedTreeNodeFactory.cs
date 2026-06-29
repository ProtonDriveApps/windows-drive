using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Synced;

public class SyncedTreeNodeFactory<TId> : IFileSystemNodeFactory<SyncedTree<TId>, SyncedTreeNode<TId>, SyncedTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    public SyncedTreeNode<TId> CreateRootNode(SyncedTree<TId> tree)
    {
        return CreateNode(tree, new SyncedTreeNodeModel<TId>(), default);
    }

    public SyncedTreeNode<TId> CreateNode(
        SyncedTree<TId> tree,
        SyncedTreeNodeModel<TId> model,
        SyncedTreeNode<TId>? parent)
    {
        return new SyncedTreeNode<TId>(tree, model, parent);
    }
}
