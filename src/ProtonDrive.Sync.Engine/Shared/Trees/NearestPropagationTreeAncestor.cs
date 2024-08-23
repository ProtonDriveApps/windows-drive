using System;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.Sync.Engine.Shared.Trees;

internal class NearestPropagationTreeAncestor<TId>
    where TId : IEquatable<TId>
{
    private readonly SyncedTree<TId> _syncedTree;
    private readonly PropagationTree<TId> _propagationTree;

    public NearestPropagationTreeAncestor(SyncedTree<TId> syncedTree, PropagationTree<TId> propagationTree)
    {
        _syncedTree = syncedTree;
        _propagationTree = propagationTree;
    }

    public bool IsDeleted(IIdentifiableTreeNode<TId> nodeModel)
    {
        var ancestorNode = NearestAncestor(nodeModel);

        // Looking for the ancestor with the Deleted status
        while (!ancestorNode.IsRoot)
        {
            if (ancestorNode.Model.RemoteStatus.Contains(UpdateStatus.Deleted) ||
                ancestorNode.Model.LocalStatus.Contains(UpdateStatus.Deleted))
            {
                return true;
            }

            ancestorNode = ancestorNode.Parent!;
        }

        return false;
    }

    private PropagationTreeNode<TId> NearestAncestor(IIdentifiableTreeNode<TId> nodeModel)
    {
        var node = _propagationTree.NodeByIdOrDefault(nodeModel.ParentId);
        if (node != null)
        {
            return node;
        }

        var syncedNode = _syncedTree.NodeByIdOrDefault(nodeModel.ParentId);
        if (syncedNode == null)
        {
            throw new InvalidOperationException();
        }

        PropagationTreeNode<TId>? parentNode = null;

        while (parentNode == null && !syncedNode.IsRoot)
        {
            syncedNode = syncedNode.Parent;
            parentNode = _propagationTree.NodeByIdOrDefault(syncedNode!.Id);
        }

        return parentNode ?? _propagationTree.Root;
    }
}
