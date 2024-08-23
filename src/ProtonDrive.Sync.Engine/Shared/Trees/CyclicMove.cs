using System;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.Sync.Engine.Shared.Trees;

internal class CyclicMove<TId>
    where TId : IEquatable<TId>
{
    private readonly SyncedTree<TId> _syncedTree;
    private readonly PropagationTree<TId> _propagationTree;

    public CyclicMove(SyncedTree<TId> syncedTree, PropagationTree<TId> propagationTree)
    {
        _syncedTree = syncedTree;
        _propagationTree = propagationTree;
    }

    public bool Exists(IIdentifiableTreeNode<TId> nodeModel)
    {
        var propagationNode = _propagationTree.NodeByIdOrDefault(nodeModel.Id);
        if (propagationNode == null)
        {
            return false;
        }

        var parentNode = _propagationTree.NodeByIdOrDefault(nodeModel.ParentId);
        if (parentNode == null)
        {
            var syncedParentNode = _syncedTree.NodeByIdOrDefault(nodeModel.ParentId) ??
                                   throw new InvalidOperationException($"SyncedTree node with Id={nodeModel.ParentId} does not exist");

            while (parentNode == null)
            {
                syncedParentNode = syncedParentNode.Parent;
                if (syncedParentNode!.IsRoot)
                {
                    return false;
                }

                parentNode = _propagationTree.NodeByIdOrDefault(syncedParentNode.Id);
            }
        }

        while (!parentNode!.IsRoot)
        {
            if (parentNode == propagationNode)
            {
                return true;
            }

            parentNode = parentNode.Parent;
        }

        return false;
    }
}
