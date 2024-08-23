using System;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.Sync.Engine.ConflictResolution;

internal class SyncRootLocator<TId>
    where TId : IEquatable<TId>
{
    private readonly SyncedTree<TId> _syncedTree;
    private readonly PropagationTree<TId> _propagationTree;

    public SyncRootLocator(SyncedTree<TId> syncedTree, PropagationTree<TId> propagationTree)
    {
        _syncedTree = syncedTree;
        _propagationTree = propagationTree;
    }

    public TId GetSyncRootNodeId(IIdentifiableTreeNode<TId> nodeModel)
    {
        var node = _propagationTree.NodeByIdOrDefault(nodeModel.ParentId);

        if (node == null)
        {
            var syncedNode = _syncedTree.NodeById(nodeModel.ParentId);

            while (node == null)
            {
                if (syncedNode.IsRoot)
                {
                    throw new InvalidOperationException("Cannot find sync root node");
                }

                if (syncedNode.IsSyncRoot())
                {
                    return syncedNode.Id;
                }

                syncedNode = syncedNode.Parent;
                node = _propagationTree.NodeByIdOrDefault(syncedNode!.Id);
            }
        }

        while (!node.IsSyncRoot())
        {
            if (node.IsRoot)
            {
                throw new InvalidOperationException("Cannot find sync root node");
            }

            node = node.Parent;
        }

        return node.Id;
    }
}
