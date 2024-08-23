using System;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.Shared.Trees.Propagation;

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
