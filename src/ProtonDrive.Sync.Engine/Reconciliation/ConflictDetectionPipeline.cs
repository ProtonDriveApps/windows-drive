using System;
using System.Diagnostics.CodeAnalysis;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.Reconciliation;

internal class ConflictDetectionPipeline<TId>
    where TId : IEquatable<TId>
{
    private readonly UpdateTree<TId> _remoteUpdateTree;
    private readonly UpdateTree<TId> _localUpdateTree;

    private readonly CyclicMove<TId> _cyclicMove;
    private readonly NameClash<TId> _nameClash;
    private readonly NearestPropagationTreeAncestor<TId> _nearestAncestor;

    public ConflictDetectionPipeline(
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> remoteUpdateTree,
        UpdateTree<TId> localUpdateTree,
        PropagationTree<TId> propagationTree)
    {
        _remoteUpdateTree = remoteUpdateTree;
        _localUpdateTree = localUpdateTree;

        _cyclicMove = new CyclicMove<TId>(syncedTree, propagationTree);
        _nameClash = new NameClash<TId>(syncedTree, propagationTree);
        _nearestAncestor = new NearestPropagationTreeAncestor<TId>(syncedTree, propagationTree);
    }

    public ConflictType IndirectConflict(UpdateTreeNodeModel<TId> nodeModel)
    {
        var status = nodeModel.Status;

        // Move-ParentDelete (Dest)
        if ((status.Contains(UpdateStatus.Moved) || status.Contains(UpdateStatus.Renamed)) &&
            DestinationParentDeleted(nodeModel))
        {
            return ConflictType.MoveParentDeleteDest;
        }

        // Create-ParentDelete
        if (status.Contains(UpdateStatus.Created) && DestinationParentDeleted(nodeModel))
        {
            return ConflictType.CreateParentDelete;
        }

        // Move-Move (Cycle)
        if (status.Contains(UpdateStatus.Moved) && CyclicMove(nodeModel))
        {
            return ConflictType.MoveMoveCycle;
        }

        return ConflictType.None;
    }

    public ConflictType MoveConflict(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel)
    {
        var remoteStatus = remoteNodeModel.Status;
        var localStatus = localNodeModel.Status;

        // Move-Move (Source)

        if (remoteStatus.Contains(UpdateStatus.Renamed) &&
            localStatus.Contains(UpdateStatus.Renamed) &&
            localNodeModel.Name != remoteNodeModel.Name)
        {
            // Moved to different names
            return ConflictType.MoveMoveSource;
        }

        if (remoteStatus.Contains(UpdateStatus.Moved) &&
            localStatus.Contains(UpdateStatus.Moved) &&
            !localNodeModel.ParentId.Equals(remoteNodeModel.ParentId))
        {
            // Moved to different parents
            return ConflictType.MoveMoveSource;
        }

        return ConflictType.None;
    }

    public ConflictType EditConflict(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel)
    {
        var remoteStatus = remoteNodeModel.Status;
        var localStatus = localNodeModel.Status;

        // Edit-Edit
        if (remoteNodeModel.Type == NodeType.File && localNodeModel.Type == NodeType.File &&
            remoteStatus.Contains(UpdateStatus.Edited) && localStatus.Contains(UpdateStatus.Edited))
        {
            return ConflictType.EditEdit;
        }

        return ConflictType.None;
    }

    public ConflictType DeleteConflict(
        PropagationTreeNodeModel<TId> nodeModel)
    {
        var remoteStatus = nodeModel.RemoteStatus;
        var localStatus = nodeModel.LocalStatus;

        // Move-Delete
        if ((remoteStatus.Contains(UpdateStatus.Renamed) || remoteStatus.Contains(UpdateStatus.Moved)) &&
            localStatus.Contains(UpdateStatus.Deleted) ||
            (localStatus.Contains(UpdateStatus.Renamed) || localStatus.Contains(UpdateStatus.Moved)) &&
            remoteStatus.Contains(UpdateStatus.Deleted))
        {
            return ConflictType.MoveDelete;
        }

        if (remoteStatus.Contains(UpdateStatus.Edited) && localStatus.Contains(UpdateStatus.Deleted) ||
            remoteStatus.Contains(UpdateStatus.Deleted) && localStatus.Contains(UpdateStatus.Edited))
        {
            // Edit-ParentDelete
            if (RemoteParentDeleted(nodeModel) || LocalParentDeleted(nodeModel))
            {
                return ConflictType.EditParentDelete;
            }

            // Edit-Delete
            return ConflictType.EditDelete;
        }

        return ConflictType.None;
    }

    public (ConflictType ConflictType, PropagationTreeNodeModel<TId> OtherNodeModel) NameClashConflict(PropagationTreeNodeModel<TId> nodeModel)
    {
        var localStatus = nodeModel.LocalStatus;
        var remoteStatus = nodeModel.RemoteStatus;

        if ((localStatus.Contains(UpdateStatus.Created) || remoteStatus.Contains(UpdateStatus.Created) ||
             localStatus.Contains(UpdateStatus.Renamed) || remoteStatus.Contains(UpdateStatus.Renamed) ||
             localStatus.Contains(UpdateStatus.Moved) || remoteStatus.Contains(UpdateStatus.Moved)) &&
            NameClash(nodeModel, out var conflictingNodeModel))
        {
            if ((localStatus.Contains(UpdateStatus.Created) || remoteStatus.Contains(UpdateStatus.Created)) &&
                (conflictingNodeModel.LocalStatus.Contains(UpdateStatus.Created) ||
                 conflictingNodeModel.RemoteStatus.Contains(UpdateStatus.Created)))
            {
                return (ConflictType.CreateCreate, conflictingNodeModel);
            }

            if ((localStatus.Contains(UpdateStatus.Created) || remoteStatus.Contains(UpdateStatus.Created)) &&
                (conflictingNodeModel.LocalStatus.Contains(UpdateStatus.Renamed) ||
                 conflictingNodeModel.RemoteStatus.Contains(UpdateStatus.Renamed) ||
                 conflictingNodeModel.LocalStatus.Contains(UpdateStatus.Moved) ||
                 conflictingNodeModel.RemoteStatus.Contains(UpdateStatus.Moved)))
            {
                return (ConflictType.MoveCreate, conflictingNodeModel);
            }

            if ((localStatus.Contains(UpdateStatus.Renamed) || remoteStatus.Contains(UpdateStatus.Renamed) ||
                 localStatus.Contains(UpdateStatus.Moved) || remoteStatus.Contains(UpdateStatus.Moved)) &&
                (conflictingNodeModel.LocalStatus.Contains(UpdateStatus.Created) ||
                 conflictingNodeModel.RemoteStatus.Contains(UpdateStatus.Created)))
            {
                return (ConflictType.MoveCreate, conflictingNodeModel);
            }

            return (ConflictType.MoveMoveDest, conflictingNodeModel);
        }

        return (ConflictType.None, new PropagationTreeNodeModel<TId>());
    }

    private bool DestinationParentDeleted(IIdentifiableTreeNode<TId> nodeModel)
    {
        return _nearestAncestor.IsDeleted(nodeModel);
    }

    private bool CyclicMove(IIdentifiableTreeNode<TId> nodeModel)
    {
        return _cyclicMove.Exists(nodeModel);
    }

    private bool NameClash(
        PropagationTreeNodeModel<TId> nodeModel,
        [NotNullWhen(true)]
        out PropagationTreeNodeModel<TId>? conflictingNodeModel)
    {
        return _nameClash.Exists(nodeModel, out conflictingNodeModel);
    }

    private bool RemoteParentDeleted(PropagationTreeNodeModel<TId> nodeModel)
    {
        if (!nodeModel.LocalStatus.Contains(UpdateStatus.Deleted))
        {
            return false;
        }

        var node = _remoteUpdateTree.NodeByIdOrDefault(nodeModel.AltId);

        // Deleted status implies that either the node itself or the parent node is deleted.
        // If node itself is not deleted, then the parent node is deleted.
        return node?.Model.Status.Contains(UpdateStatus.Deleted) != true;
    }

    private bool LocalParentDeleted(PropagationTreeNodeModel<TId> nodeModel)
    {
        if (nodeModel.RemoteStatus != UpdateStatus.Deleted)
        {
            return false;
        }

        var node = _localUpdateTree.NodeByIdOrDefault(nodeModel.Id);

        // Deleted status implies that either the node itself or the parent node is deleted.
        // If node itself is not deleted, then the parent node is deleted.
        return node?.Model.Status.Contains(UpdateStatus.Deleted) != true;
    }
}
