using System;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.Trees.Adapter;

internal class AdapterTree<TId, TAltId> : LooseCompoundAltIdentifiableFileSystemTree<AdapterTree<TId, TAltId>, AdapterTreeNode<TId, TAltId>, AdapterTreeNodeModel<TId, TAltId>, TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    public AdapterTree(
        ILooseCompoundAltIdentifiableTreeNodeRepository<AdapterTreeNodeModel<TId, TAltId>, TId, TAltId> repository,
        IFileSystemNodeFactory<AdapterTree<TId, TAltId>, AdapterTreeNode<TId, TAltId>, AdapterTreeNodeModel<TId, TAltId>, TId> factory)
        : base(repository, factory)
    {
    }
}
