using Proton.Drive.Sdk.Sync.Shared.Trees;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Synced;

public class SyncedTree<TId> : AltIdentifiableFileSystemTree<SyncedTree<TId>, SyncedTreeNode<TId>, SyncedTreeNodeModel<TId>, TId, TId>
    where TId : IEquatable<TId>
{
    public SyncedTree(
        IAltIdentifiableTreeNodeRepository<SyncedTreeNodeModel<TId>, TId, TId> repository,
        IFileSystemNodeFactory<SyncedTree<TId>, SyncedTreeNode<TId>, SyncedTreeNodeModel<TId>, TId> factory)
        : base(repository, factory)
    {
    }
}
