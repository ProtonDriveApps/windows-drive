using System;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.Shared.Trees.Synced;

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
