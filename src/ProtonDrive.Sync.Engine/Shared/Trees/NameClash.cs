using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.Shared.Trees;

internal class NameClash<TId>
    where TId : IEquatable<TId>
{
    private readonly SyncedTree<TId> _syncedTree;
    private readonly PropagationTree<TId> _propagationTree;

    public NameClash(SyncedTree<TId> syncedTree, PropagationTree<TId> propagationTree)
    {
        _syncedTree = syncedTree;
        _propagationTree = propagationTree;
    }

    public bool Exists(
        PropagationTreeNodeModel<TId> nodeModel,
        [NotNullWhen(true)]
        out PropagationTreeNodeModel<TId>? conflictingNodeModel)
    {
        var conflictingNode = ConflictingPropagationTreeNode(nodeModel);

        if (conflictingNode != null)
        {
            conflictingNodeModel = conflictingNode.Model;
            return true;
        }

        // If node was renamed on one replica and moved to another parent on other replica,
        // then it might conflict with existing not changed node. Need to look into Synced Tree
        // for conflicting nodes.

        if (nodeModel.LocalStatus.Contains(UpdateStatus.Renamed) &&
            nodeModel.RemoteStatus.Contains(UpdateStatus.Moved) ||
            nodeModel.LocalStatus.Contains(UpdateStatus.Moved) &&
            nodeModel.RemoteStatus.Contains(UpdateStatus.Renamed))
        {
            var conflictingSyncedNode = ConflictingSyncedTreeNode(nodeModel);

            if (conflictingSyncedNode != null)
            {
                conflictingNodeModel = new PropagationTreeNodeModel<TId>()
                    .CopiedFrom(conflictingSyncedNode.Model)
                    .WithAltId(conflictingSyncedNode.AltId);

                return true;
            }
        }

        conflictingNodeModel = default;
        return false;
    }

    public bool Exists(IFileSystemNodeModel<TId> nodeModel)
    {
        var conflictingNode = ConflictingPropagationTreeNode(nodeModel);

        return conflictingNode != null;
    }

    private PropagationTreeNode<TId>? ConflictingPropagationTreeNode(IFileSystemNodeModel<TId> nodeModel)
    {
        var parentNode = _propagationTree.NodeByIdOrDefault(nodeModel.ParentId);

        var conflictingNodes = parentNode?
            .ChildrenByName(nodeModel.Name)
            .Where(node => !node.Id.Equals(nodeModel.Id));

        var conflictingNode = conflictingNodes?
            .FirstOrDefault(node => !IsDeleted(node));

        return conflictingNode;
    }

    private SyncedTreeNode<TId>? ConflictingSyncedTreeNode(IFileSystemNodeModel<TId> nodeModel)
    {
        var syncedParentNode = _syncedTree.NodeByIdOrDefault(nodeModel.ParentId);

        var syncedConflictingNodes = syncedParentNode?
            .ChildrenByName(nodeModel.Name)
            .Where(node => !node.Id.Equals(nodeModel.Id));

        var syncedConflictingNode = syncedConflictingNodes?
            .FirstOrDefault(node => !ExistsInPropagationTree(node));

        return syncedConflictingNode;
    }

    private bool IsDeleted(PropagationTreeNode<TId> node)
    {
        return node.Model.RemoteStatus.Contains(UpdateStatus.Deleted) ||
               node.Model.LocalStatus.Contains(UpdateStatus.Deleted);
    }

    private bool ExistsInPropagationTree(SyncedTreeNode<TId> node)
    {
        return _propagationTree.NodeByIdOrDefault(node.Id) != null;
    }
}
