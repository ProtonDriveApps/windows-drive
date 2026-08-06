using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Update;

public class UpdateTreeNodeFactory<TId> : IFileSystemNodeFactory<UpdateTree<TId>, UpdateTreeNode<TId>, UpdateTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    public UpdateTreeNode<TId> CreateRootNode(UpdateTree<TId> tree)
    {
        return CreateNode(tree, new UpdateTreeNodeModel<TId>(), default);
    }

    public UpdateTreeNode<TId> CreateNode(
        UpdateTree<TId> tree,
        UpdateTreeNodeModel<TId> model,
        UpdateTreeNode<TId>? parent)
    {
        return new UpdateTreeNode<TId>(tree, model, parent);
    }
}
