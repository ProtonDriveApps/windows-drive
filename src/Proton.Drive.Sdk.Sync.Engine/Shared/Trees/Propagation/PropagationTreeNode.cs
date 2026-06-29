using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Propagation;

internal class PropagationTreeNode<TId> : FileSystemNode<PropagationTree<TId>, PropagationTreeNode<TId>, PropagationTreeNodeModel<TId>, TId>
    where TId : IEquatable<TId>
{
    protected internal PropagationTreeNode(
        PropagationTree<TId> tree,
        PropagationTreeNodeModel<TId> model,
        PropagationTreeNode<TId>? parent)
        : base(tree, model, parent)
    {
    }

    public TId AltId => Model.AltId;
}
