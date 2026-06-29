using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Propagation;

internal class PropagationTreeNodeFactory<TId> : IFileSystemNodeFactory<PropagationTree<TId>, PropagationTreeNode<TId>, PropagationTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    public PropagationTreeNode<TId> CreateRootNode(PropagationTree<TId> tree)
    {
        return CreateNode(tree, new PropagationTreeNodeModel<TId>(), default);
    }

    public PropagationTreeNode<TId> CreateNode(
        PropagationTree<TId> tree,
        PropagationTreeNodeModel<TId> model,
        PropagationTreeNode<TId>? parent)
    {
        return new PropagationTreeNode<TId>(tree, model, parent);
    }
}
