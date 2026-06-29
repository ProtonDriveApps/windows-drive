using Proton.Drive.Sdk.Sync.Shared.Trees;
using Proton.Drive.Sdk.Sync.Shared.Trees.Collections;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Update;

public class UpdateTree<TId> : FileSystemTree<UpdateTree<TId>, UpdateTreeNode<TId>, UpdateTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    public UpdateTree(
        ITreeNodeRepository<UpdateTreeNodeModel<TId>, TId> repository,
        IFileSystemNodeFactory<UpdateTree<TId>, UpdateTreeNode<TId>, UpdateTreeNodeModel<TId>, TId> factory)
        : base(repository, factory, new IdentifiableNodeDictionary<UpdateTreeNode<TId>, UpdateTreeNodeModel<TId>, TId>())
    {
    }
}
