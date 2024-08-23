using System;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Engine.ConflictResolution;
using ProtonDrive.Sync.Engine.Reconciliation;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.Propagation;

internal class PseudoConflictPropagationPipeline<TId>
    where TId : IEquatable<TId>
{
    private readonly UpdateTree<TId> _remoteUpdateTree;
    private readonly UpdateTree<TId> _localUpdateTree;
    private readonly PropagationTree<TId> _propagationTree;

    private readonly PreparationPipeline<TId> _preparation;
    private readonly PseudoConflictDetectionPipeline<TId> _pseudoConflictDetection;
    private readonly PseudoConflictResolutionPipeline<TId> _pseudoConflictResolution;

    public PseudoConflictPropagationPipeline(
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> remoteUpdateTree,
        UpdateTree<TId> localUpdateTree,
        PropagationTree<TId> propagationTree)
    {
        _remoteUpdateTree = remoteUpdateTree;
        _localUpdateTree = localUpdateTree;
        _propagationTree = propagationTree;

        _preparation = new PreparationPipeline<TId>(syncedTree, remoteUpdateTree, localUpdateTree);
        _pseudoConflictDetection = new PseudoConflictDetectionPipeline<TId>(syncedTree, remoteUpdateTree, localUpdateTree, propagationTree);
        _pseudoConflictResolution = new PseudoConflictResolutionPipeline<TId>(syncedTree, remoteUpdateTree, localUpdateTree);
    }

    public void Execute(
        PropagationTreeNode<TId> node,
        Func<PropagationTreeNodeModel<TId>, UpdateStatus, UpdateStatus> filter)
    {
        var (remoteNodeModel, localNodeModel) = Prepare(node, filter);

        ResolvePseudoConflicts(node, remoteNodeModel, localNodeModel);
    }

    private (UpdateTreeNodeModel<TId>? Remote, UpdateTreeNodeModel<TId>? Local) Prepare(
        PropagationTreeNode<TId> node,
        Func<PropagationTreeNodeModel<TId>, UpdateStatus, UpdateStatus> filter)
    {
        var conflictingStatus = filter(node.Model, node.Model.RemoteStatus.Intersect(node.Model.LocalStatus));
        if (conflictingStatus == UpdateStatus.Unchanged)
        {
            return (null, null);
        }

        var remoteNode = _remoteUpdateTree.NodeByIdOrDefault(node.AltId);
        if (remoteNode == null)
        {
            return (null, null);
        }

        var localNode = _localUpdateTree.NodeByIdOrDefault(node.Id);
        if (localNode == null)
        {
            return (null, null);
        }

        var renamed = localNode.Model.Name != remoteNode.Model.Name || localNode.Model.Name != node.Model.Name;
        var moved = !localNode.Model.ParentId.Equals(remoteNode.Model.ParentId) || !localNode.Model.ParentId.Equals(node.Model.ParentId);

        conflictingStatus = conflictingStatus
            .Intersect(remoteNode.Model.Status)
            .Intersect(localNode.Model.Status)
            .Minus(renamed ? UpdateStatus.Renamed : UpdateStatus.Unchanged)
            .Minus(moved ? UpdateStatus.Moved : UpdateStatus.Unchanged);
        if (conflictingStatus == UpdateStatus.Unchanged)
        {
            return (null, null);
        }

        var (remoteNodeModel, localNodeModel) = _preparation.Execute(remoteNode, localNode);

        remoteNodeModel = remoteNodeModel.WithStatus(conflictingStatus);
        localNodeModel = localNodeModel.WithStatus(conflictingStatus);

        return (remoteNodeModel, localNodeModel);
    }

    private void ResolvePseudoConflicts(
        PropagationTreeNode<TId> node,
        UpdateTreeNodeModel<TId>? remoteNodeModel,
        UpdateTreeNodeModel<TId>? localNodeModel)
    {
        if (remoteNodeModel == null || localNodeModel == null)
        {
            return;
        }

        foreach (var (conflictType, conflictingStatus) in _pseudoConflictDetection.PseudoConflict(remoteNodeModel, localNodeModel))
        {
            _pseudoConflictResolution.Execute(remoteNodeModel, localNodeModel, conflictType, conflictingStatus);

            var renamed = localNodeModel.Name != remoteNodeModel.Name;
            var moved = !localNodeModel.ParentId.Equals(remoteNodeModel.ParentId);
            var resolvedStatus = conflictingStatus
                .Minus(renamed ? UpdateStatus.Renamed : UpdateStatus.Unchanged)
                .Minus(moved ? UpdateStatus.Moved : UpdateStatus.Unchanged);

            _propagationTree.Operations.Execute(new Operation<PropagationTreeNodeModel<TId>>(
                OperationType.Update,
                node.Model.Copy()
                    .WithRemoteStatus(node.Model.RemoteStatus.Minus(resolvedStatus))
                    .WithLocalStatus(node.Model.LocalStatus.Minus(resolvedStatus))));
        }
    }
}
