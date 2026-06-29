using Proton.Drive.Sdk.Sync.Shared.Trees;
using Proton.Drive.Sdk.Sync.Shared.Trees.Collections;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Adapter.Trees.Dirty;

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
