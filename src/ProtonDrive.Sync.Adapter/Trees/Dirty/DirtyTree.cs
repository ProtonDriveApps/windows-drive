using System;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.Collections;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.Trees.Dirty;

internal class DirtyTree<TId> : FileSystemTree<DirtyTree<TId>, DirtyTreeNode<TId>, DirtyTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    public DirtyTree(
        ITreeNodeRepository<DirtyTreeNodeModel<TId>, TId> repository,
        IFileSystemNodeFactory<DirtyTree<TId>, DirtyTreeNode<TId>, DirtyTreeNodeModel<TId>, TId> factory)
        : base(repository, factory, new IdentifiableNodeDictionary<DirtyTreeNode<TId>, DirtyTreeNodeModel<TId>, TId>())
    {
    }
}
