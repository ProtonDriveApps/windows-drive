using System;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;

namespace ProtonDrive.Sync.Engine.Consolidation;

internal class NearestUpdateTreeParent<TId>
    where TId : IEquatable<TId>
{
    private readonly Replica _replica;
    private readonly UpdateTree<TId> _updateTree;

    public NearestUpdateTreeParent(Replica replica, UpdateTree<TId> updateTree)
    {
        _replica = replica;
        _updateTree = updateTree;
    }

    public UpdateTreeNode<TId>? NearestParent(
        UpdateTreeNode<TId> node,
        SyncedTreeNode<TId> syncedNode)
    {
        if (node.Model.ParentId.Equals(syncedNode.Parent!.Model.OwnId(_replica)))
        {
            return node.Parent;
        }

        UpdateTreeNode<TId>? parentNode = null;

        while (parentNode == null && !syncedNode.IsRoot)
        {
            syncedNode = syncedNode.Parent!;
            parentNode = _updateTree.NodeByIdOrDefault(syncedNode.Model.OwnId(_replica));
        }

        return parentNode;
    }
}
