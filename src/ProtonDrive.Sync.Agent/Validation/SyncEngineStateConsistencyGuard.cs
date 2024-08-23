using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Trees.FileSystem.Traversal;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Agent.Validation;

public sealed class SyncEngineStateConsistencyGuard<TId>
    where TId : IEquatable<TId>
{
    private readonly SyncedTree<TId> _syncedTree;
    private readonly UpdateTree<TId> _localUpdateTree;
    private readonly UpdateTree<TId> _remoteUpdateTree;
    private readonly IScheduler _syncScheduler;
    private readonly ILogger<SyncEngineStateConsistencyGuard<TId>> _logger;

    private readonly PassiveTreeTraversal<UpdateTree<TId>, UpdateTreeNode<TId>, UpdateTreeNodeModel<TId>, TId>
        _updateTreeTraversal = new();

    public SyncEngineStateConsistencyGuard(
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> localUpdateTree,
        UpdateTree<TId> remoteUpdateTree,
        IScheduler syncScheduler,
        ILogger<SyncEngineStateConsistencyGuard<TId>> logger)
    {
        _syncedTree = syncedTree;
        _localUpdateTree = localUpdateTree;
        _remoteUpdateTree = remoteUpdateTree;
        _syncScheduler = syncScheduler;
        _logger = logger;
    }

    public Task VerifyAndFixStateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Checking Sync Engine state consistency");

        return _syncScheduler.Schedule(() => VerifyAndFixState(cancellationToken), cancellationToken);
    }

    private void VerifyAndFixState(CancellationToken cancellationToken)
    {
        VerifyAndFixUpdateTree(Replica.Local, cancellationToken);
        VerifyAndFixUpdateTree(Replica.Remote, cancellationToken);
    }

    private void VerifyAndFixUpdateTree(Replica replica, CancellationToken cancellationToken)
    {
        var updateTree = GetUpdateTree(replica);

        foreach (var node in _updateTreeTraversal.ExcludeStartingNode().PreOrder(updateTree.Root, cancellationToken))
        {
            var syncedNode = _syncedTree.NodeByOwnIdOrDefault(node.Id, replica);

            var nodeStateIsValid =
                HandleExistingSyncedNode(replica, node, syncedNode) &&
                HandleMissingSyncedNode(replica, node, syncedNode);

            if (!nodeStateIsValid)
            {
                _updateTreeTraversal.SkipChildren();
            }
        }
    }

    private bool HandleExistingSyncedNode(Replica replica, UpdateTreeNode<TId> node, SyncedTreeNode<TId>? syncedNode)
    {
        if (syncedNode == null)
        {
            return true;
        }

        // Created Update Tree node must have no corresponding Synced Tree node
        if (!node.Model.Status.HasFlag(UpdateStatus.Created))
        {
            return true;
        }

        _logger.LogError(
            "{Replica} Update Tree node with Id={NodeId} (parent Id={ParentId}) and Status=({NodeStatus}) has corresponding Synced Tree node with parent Id={SyncedParentId}",
            replica,
            node.Id,
            node.Model.ParentId,
            node.Model.Status,
            syncedNode.Model.ParentId);

        // The state of the Update Tree node is invalid
        return false;
    }

    private bool HandleMissingSyncedNode(Replica replica, UpdateTreeNode<TId> node, SyncedTreeNode<TId>? syncedNode)
    {
        if (syncedNode != null)
        {
            return true;
        }

        // The Update Tree node (except Created nodes) must have corresponding Synced Tree node
        if (node.Model.Status.HasFlag(UpdateStatus.Created))
        {
            return true;
        }

        _logger.LogError(
            "{Replica} Update Tree node with Id={NodeId} (parent Id={ParentId}) and Status=({NodeStatus}) has no corresponding Synced Tree node",
            replica,
            node.Id,
            node.Model.ParentId,
            node.Model.Status);

        // We can fix Moved nodes only
        if (!node.Model.Status.HasFlag(UpdateStatus.Moved))
        {
            // The state the Update Tree node is invalid
            return false;
        }

        // We turn Moved node into Created node, for the Sync Engine to re-create it on the other replica
        GetUpdateTree(replica).Operations.Execute(
            new Operation<UpdateTreeNodeModel<TId>>(
                OperationType.Update,
                node.Model.Copy().WithStatus(UpdateStatus.Created | UpdateStatus.Restore)));

        _logger.LogWarning(
            "{Replica} Update Tree node with Id={NodeId} was fixed by changing status to ({NodeStatus})",
            replica,
            node.Id,
            node.Model.Status);

        return true;
    }

    private UpdateTree<TId> GetUpdateTree(Replica replica)
    {
        return replica is Replica.Local ? _localUpdateTree : _remoteUpdateTree;
    }
}
