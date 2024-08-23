using System;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.ConflictResolution;

internal class PseudoConflictResolutionPipeline<TId>
    where TId : IEquatable<TId>
{
    private readonly SyncedTree<TId> _syncedTree;
    private readonly UpdateTree<TId> _remoteUpdateTree;
    private readonly UpdateTree<TId> _localUpdateTree;

    private readonly UnchangedUpdateTreeLeavesRemovalOperationsFactory<TId> _unchangedLeavesRemoval;

    public PseudoConflictResolutionPipeline(
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> remoteUpdateTree,
        UpdateTree<TId> localUpdateTree)
    {
        _syncedTree = syncedTree;
        _remoteUpdateTree = remoteUpdateTree;
        _localUpdateTree = localUpdateTree;

        _unchangedLeavesRemoval = new UnchangedUpdateTreeLeavesRemovalOperationsFactory<TId>();
    }

    public (UpdateTreeNodeModel<TId> RemoteNodeModel, UpdateTreeNodeModel<TId> LocalNodeModel) Execute(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel,
        ConflictType conflictType,
        UpdateStatus conflictingStatus)
    {
        if (conflictType == ConflictType.None)
        {
            return (remoteNodeModel, localNodeModel);
        }

        switch (conflictType)
        {
            case ConflictType.CreateCreatePseudo:
                if (conflictingStatus != UpdateStatus.Created)
                {
                    throw new InvalidOperationException();
                }

                return ResolvePseudoConflict(remoteNodeModel, localNodeModel, OperationType.Create, conflictingStatus);

            case ConflictType.MoveMovePseudo:
                if (conflictingStatus.Intersect(UpdateStatus.RenamedAndMoved) != conflictingStatus)
                {
                    throw new InvalidOperationException();
                }

                return ResolvePseudoConflict(remoteNodeModel, localNodeModel, OperationType.Move, conflictingStatus);

            case ConflictType.EditEditPseudo:
                if (conflictingStatus != UpdateStatus.Edited)
                {
                    throw new InvalidOperationException();
                }

                return ResolvePseudoConflict(remoteNodeModel, localNodeModel, OperationType.Edit, conflictingStatus);

            case ConflictType.DeleteDeletePseudo:
                if (!conflictingStatus.Contains(UpdateStatus.Deleted))
                {
                    throw new InvalidOperationException();
                }

                return ResolvePseudoConflict(remoteNodeModel, localNodeModel, OperationType.Delete, conflictingStatus);

            default:
                throw new InvalidOperationException($"Invalid {nameof(conflictType)} value {conflictType}");
        }
    }

    private (UpdateTreeNodeModel<TId> RemoteNodeModel, UpdateTreeNodeModel<TId> LocalNodeModel) ResolvePseudoConflict(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel,
        OperationType operationType,
        UpdateStatus conflictingStatus)
    {
        if (conflictingStatus == UpdateStatus.Unchanged)
        {
            return (remoteNodeModel, localNodeModel);
        }

        AdjustRemoteUpdateTree(remoteNodeModel, conflictingStatus);

        AdjustLocalUpdateTree(localNodeModel, conflictingStatus);

        AdjustSyncedTree(remoteNodeModel, localNodeModel, operationType);

        return (AdjustedNodeModel(remoteNodeModel, conflictingStatus),
            AdjustedNodeModel(localNodeModel, conflictingStatus));
    }

    private void AdjustSyncedTree(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel,
        OperationType operationType)
    {
        var model = new SyncedTreeNodeModel<TId>()
            .CopiedFrom(localNodeModel)
            .WithAltId(remoteNodeModel.Id);

        if (operationType != OperationType.Create && operationType != OperationType.Delete)
        {
            var syncedNode = _syncedTree.NodeByIdOrDefault(localNodeModel.Id);
            if (syncedNode == null)
            {
                throw new InvalidOperationException($"SyncedTree node with Id={localNodeModel.Id} does not exist");
            }

            if (operationType == OperationType.Move)
            {
                if (remoteNodeModel.Name != localNodeModel.Name)
                {
                    model = model.WithName<SyncedTreeNodeModel<TId>, TId>(syncedNode.Model.Name);
                }

                if (!remoteNodeModel.ParentId.Equals(localNodeModel.ParentId))
                {
                    model = model.WithParentId(syncedNode.Model.ParentId);
                }
            }

            model = model.WithAltId(syncedNode.AltId);
        }

        _syncedTree.Operations.Execute(new Operation<SyncedTreeNodeModel<TId>>(operationType, model));
    }

    private void AdjustRemoteUpdateTree(UpdateTreeNodeModel<TId> nodeModel, UpdateStatus conflictingStatus)
    {
        // SyncedTree node doesn't exist for Created nodes
        var syncedNode = _syncedTree.NodeByIdOrDefault(nodeModel.Id);
        var node = _remoteUpdateTree.NodeByIdOrDefault(syncedNode != null ? syncedNode.AltId : nodeModel.Id);

        // Missing Update Tree node indicates the parent was deleted in this replica, no need to adjust.
        if (node == null)
        {
            return;
        }

        AdjustUpdateTree(node, conflictingStatus, Replica.Remote);
    }

    private void AdjustLocalUpdateTree(UpdateTreeNodeModel<TId> nodeModel, UpdateStatus conflictingStatus)
    {
        var node = _localUpdateTree.NodeByIdOrDefault(nodeModel.Id);

        // Missing Update Tree node indicates the parent was deleted in this replica, no need to adjust.
        if (node == null)
        {
            return;
        }

        AdjustUpdateTree(node, conflictingStatus, Replica.Local);
    }

    private void AdjustUpdateTree(
        UpdateTreeNode<TId> node,
        UpdateStatus conflictingStatus,
        Replica replica)
    {
        var status = AdjustedUpdateStatus(node.Model.Status, conflictingStatus);
        var model = node.Model.Copy().WithStatus(status);
        var tree = UpdateTreeByReplica(replica);

        tree.Operations.Execute(new Operation<UpdateTreeNodeModel<TId>>(OperationType.Update, model));

        RemoveUnchangedLeaves(node, replica);
    }

    private void RemoveUnchangedLeaves(
        UpdateTreeNode<TId> node,
        Replica replica)
    {
        var tree = UpdateTreeByReplica(replica);
        tree.Operations.Execute(_unchangedLeavesRemoval.Operations(node));
    }

    private UpdateTreeNodeModel<TId> AdjustedNodeModel(
        UpdateTreeNodeModel<TId> nodeModel,
        UpdateStatus conflictingStatus)
    {
        return nodeModel
            .WithStatus(AdjustedUpdateStatus(nodeModel.Status, conflictingStatus));
    }

    private UpdateStatus AdjustedUpdateStatus(
        UpdateStatus updateStatus,
        UpdateStatus conflictingStatus)
    {
        return updateStatus.Minus(conflictingStatus);
    }

    private UpdateTree<TId> UpdateTreeByReplica(Replica replica)
    {
        return replica == Replica.Local
            ? _localUpdateTree
            : _remoteUpdateTree;
    }
}
