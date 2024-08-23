using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Engine.Consolidation;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem.Traversal;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.Reconciliation;

internal class ReconciliationPipeline<TId>
    where TId : IComparable<TId>, IEquatable<TId>
{
    private readonly SyncedTree<TId> _syncedTree;
    private readonly UpdateTree<TId> _remoteUpdateTree;
    private readonly UpdateTree<TId> _localUpdateTree;
    private readonly PropagationTree<TId> _propagationTree;
    private readonly IScheduler _syncScheduler;
    private readonly ILogger<ReconciliationPipeline<TId>> _logger;

    private readonly Queue<UpdateTreeNodeModel<TId>> _tempUpdateTreeNodeModels = new();
    private readonly MissingUpdateTreeAncestorsFactory<TId> _missingLocalUpdateTreeAncestorsFactory;
    private readonly UpdateMergePipeline<TId> _updateMergePipeline;

    private readonly PassiveTreeTraversal<UpdateTree<TId>, UpdateTreeNode<TId>, UpdateTreeNodeModel<TId>, TId>
        _updateTreeTraversal;

    public ReconciliationPipeline(
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> remoteUpdateTree,
        UpdateTree<TId> localUpdateTree,
        PropagationTree<TId> propagationTree,
        IScheduler syncScheduler,
        IFileNameFactory<TId> nameClashConflictNameFactory,
        IFileNameFactory<TId> deleteConflictNameFactory,
        ILogger<ReconciliationPipeline<TId>> logger)
    {
        _syncedTree = syncedTree;
        _remoteUpdateTree = remoteUpdateTree;
        _localUpdateTree = localUpdateTree;
        _propagationTree = propagationTree;
        _syncScheduler = syncScheduler;
        _logger = logger;

        _missingLocalUpdateTreeAncestorsFactory = new(Replica.Local, syncedTree, localUpdateTree);
        _updateMergePipeline = new UpdateMergePipeline<TId>(
            syncedTree,
            remoteUpdateTree,
            localUpdateTree,
            propagationTree,
            nameClashConflictNameFactory,
            deleteConflictNameFactory);

        _updateTreeTraversal =
            new PassiveTreeTraversal<UpdateTree<TId>, UpdateTreeNode<TId>, UpdateTreeNodeModel<TId>, TId>();
    }

    public async Task Execute(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Started reconciliation");

        await Schedule(() => ClearPropagationTree(cancellationToken)).ConfigureAwait(false);

        await Schedule(() => CopyRemoteUpdates(cancellationToken)).ConfigureAwait(false);

        await Schedule(() => MergeLocalUpdates(cancellationToken)).ConfigureAwait(false);

        _logger.LogInformation("Finished reconciliation");
    }

    private void ClearPropagationTree(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("Clearing Propagation Tree");

        _propagationTree.Clear();
    }

    private void CopyRemoteUpdates(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("Started copying remote updates");

        foreach (var node in _updateTreeTraversal.ExcludeStartingNode().PreOrder(_remoteUpdateTree.Root, cancellationToken))
        {
            _propagationTree.Operations.Execute(new Operation<PropagationTreeNodeModel<TId>>(
                OperationType.Create,
                MappedFromRemote(node.Model)));
        }

        _logger.LogDebug("Finished copying remote updates");
    }

    private void MergeLocalUpdates(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Started merging local updates");
        cancellationToken.ThrowIfCancellationRequested();

        CacheAndClearLocalUpdates(cancellationToken);

        MoveBackAndMergeLocalUpdates(cancellationToken);
    }

    private void CacheAndClearLocalUpdates(CancellationToken cancellationToken)
    {
        _tempUpdateTreeNodeModels.Clear();

        // All except directory deletion.
        // Directory deletion involves processing child nodes, that might be moved to newly created parents,
        // which should be processed before processing any children.
        CacheLocalUpdates(FirstPassFilter, cancellationToken);

        // Directory deletion only
        CacheLocalUpdates(SecondPassFilter, cancellationToken);

        _localUpdateTree.Clear();
    }

    private void CacheLocalUpdates(Func<UpdateTreeNode<TId>, bool> filter, CancellationToken cancellationToken)
    {
        foreach (var node in _updateTreeTraversal.ExcludeStartingNode()
                     .PreOrder(_localUpdateTree.Root, cancellationToken)
                     .Where(filter)
                     .Where(ContainsChange))
        {
            _tempUpdateTreeNodeModels.Enqueue(node.Model);
        }
    }

    private bool FirstPassFilter(UpdateTreeNode<TId> node)
    {
        // All changes except directory deletion
        return !SecondPassFilter(node);
    }

    private bool SecondPassFilter(UpdateTreeNode<TId> node)
    {
        // Directory deletion only
        return node.Model.Type == NodeType.Directory && node.Model.Status.Contains(UpdateStatus.Deleted);
    }

    private bool ContainsChange(UpdateTreeNode<TId> node)
    {
        return node.Model.Status != UpdateStatus.Unchanged;
    }

    private void MoveBackAndMergeLocalUpdates(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        while (_tempUpdateTreeNodeModels.TryDequeue(out var nodeModel))
        {
            // Ancestors might not exist if there is a pseudo conflict that was resolved during reconciliation
            _localUpdateTree.Operations.Execute(WithMissingAncestors(new Operation<UpdateTreeNodeModel<TId>>(OperationType.Create, nodeModel)));
            var node = _localUpdateTree.NodeById(nodeModel.Id);

            _logger.LogDebug(
                "Merging local update: {UpdateStatus} {ParentId}/{Id} \"{Name}\"",
                node.Model.Status,
                node.Model.ParentId,
                node.Id,
                node.Name);

            MergeUpdates(null, node);
        }
    }

    private void MergeUpdates(
        UpdateTreeNode<TId>? remoteNode,
        UpdateTreeNode<TId>? localNode)
    {
        _updateMergePipeline.Execute(remoteNode, localNode);
    }

    private PropagationTreeNodeModel<TId> MappedFromRemote(UpdateTreeNodeModel<TId> nodeModel)
    {
        var syncedNode = _syncedTree.NodeByAltIdOrDefault(nodeModel.Id);
        var syncedParent = _syncedTree.NodeByAltIdOrDefault(nodeModel.ParentId);

        var id = (syncedNode != null) ? syncedNode.Id : nodeModel.Id;
        var parentId = (syncedParent != null) ? syncedParent.Id : nodeModel.ParentId;
        var altId = nodeModel.Id;

        return new PropagationTreeNodeModel<TId>()
            .CopiedFrom(nodeModel)
            .WithId(id)
            .WithParentId(parentId)
            .WithAltId(altId)
            .WithLocalStatus(nodeModel.Status);
    }

    private IEnumerable<Operation<UpdateTreeNodeModel<TId>>> WithMissingAncestors(Operation<UpdateTreeNodeModel<TId>> operation)
    {
        return _missingLocalUpdateTreeAncestorsFactory.WithMissingAncestors(operation);
    }

    private Task Schedule(Action origin)
    {
        return _syncScheduler.Schedule(origin);
    }
}
