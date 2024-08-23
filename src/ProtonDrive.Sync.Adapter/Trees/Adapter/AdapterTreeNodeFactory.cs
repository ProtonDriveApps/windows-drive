using System;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.Trees.Adapter;

internal class AdapterTreeNodeFactory<TId, TAltId> : IFileSystemNodeFactory<AdapterTree<TId, TAltId>, AdapterTreeNode<TId, TAltId>, AdapterTreeNodeModel<TId, TAltId>, TId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    public AdapterTreeNode<TId, TAltId> CreateRootNode(AdapterTree<TId, TAltId> tree)
    {
        return CreateNode(tree, new AdapterTreeNodeModel<TId, TAltId>(), default);
    }

    public AdapterTreeNode<TId, TAltId> CreateNode(
        AdapterTree<TId, TAltId> tree,
        AdapterTreeNodeModel<TId, TAltId> model,
        AdapterTreeNode<TId, TAltId>? parent)
    {
        return new AdapterTreeNode<TId, TAltId>(tree, model, parent);
    }
}
