using Proton.Drive.Sdk.Sync.Shared.Trees;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Propagation;

internal class PropagationTree<TId> : AltIdentifiableFileSystemTree<PropagationTree<TId>, PropagationTreeNode<TId>, PropagationTreeNodeModel<TId>, TId, TId>
    where TId : IEquatable<TId>
{
    public PropagationTree(
        IAltIdentifiableTreeNodeRepository<PropagationTreeNodeModel<TId>, TId, TId> repository,
        IFileSystemNodeFactory<PropagationTree<TId>, PropagationTreeNode<TId>, PropagationTreeNodeModel<TId>, TId> factory)
        : base(repository, factory)
    {
    }
}
