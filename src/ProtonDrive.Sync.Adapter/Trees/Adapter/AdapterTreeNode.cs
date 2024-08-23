using System;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.Trees.Adapter;

internal class AdapterTreeNode<TId, TAltId> : FileSystemNode<AdapterTree<TId, TAltId>, AdapterTreeNode<TId, TAltId>, AdapterTreeNodeModel<TId, TAltId>, TId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    protected internal AdapterTreeNode(
        AdapterTree<TId, TAltId> tree,
        AdapterTreeNodeModel<TId, TAltId> model,
        AdapterTreeNode<TId, TAltId>? parent)
        : base(tree, model, parent)
    {
    }

    public LooseCompoundAltIdentity<TAltId> AltId => Model.AltId;
}
