using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Engine.Consolidation;
using Proton.Drive.Sdk.Sync.Engine.Shared;
using Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Propagation;
using Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Synced;
using Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Update;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem.Traversal;
using Proton.Drive.Sdk.Sync.Shared.Trees.Operations;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Engine.Reconciliation;

internal class ReconciliationPipeline<TId>
    where TId : IComparable<TId>, IEquatable<TId>
{
    private static readonly TimeSpan SlowActionDurationThreshold = TimeSpan.FromSeconds(1);

    private readonly SyncedTree<TId> _syncedTree;
    private readonly UpdateTree<TId> _remoteUpdateTree;
    private readonly UpdateTree<TId> _localUpdateTree;
    private readonly PropagationTree<TId> _propagationTree;
    private readonly IScheduler _syncScheduler;
    private readonly ILogger<ReconciliationPipeline<TId>> _logger;

    private readonly Queue<UpdateTreeNodeModel<TId>> _detachedLocalUpdates = new();
    private readonly MissingUpdateTreeAncestorsFactory<TId> _missingLocalUpdateTreeAncestorsFactory;
    private readonly EqualizeOperationsFactory<UpdateTreeNodeModel<TId>, TId> _equalizeLocalUpdateTreeOperationsFactory;
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

        _missingLocalUpdateTreeAncestorsFactory = new MissingUpdateTreeAncestorsFactory<TId>(Replica.Local, syncedTree, localUpdateTree);
        _equalizeLocalUpdateTreeOperationsFactory = new EqualizeOperationsFactory<UpdateTreeNodeModel<TId>, TId>(
            new UpdateTreeNodeModelMetadataEqualityComparer<TId>());
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
        var startTimestamp = Stopwatch.GetTimestamp();

        await Schedule(() => ClearPropagationTree(cancellationToken)).ConfigureAwait(false);

        await Schedule(() => CopyRemoteUpdates(cancellationToken)).ConfigureAwait(false);

        await Schedule(() => MergeLocalUpdates(cancellationToken)).ConfigureAwait(false);

        _logger.LogInformation("Finished reconciliation in {ElapsedTime}", Stopwatch.GetElapsedTime(startTimestamp));
    }

    private void ClearPropagationTree(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogStart(out var startTimestamp);

        _propagationTree.Clear();

        LogSuccess();

        return;

        void LogStart(out long timestamp)
        {
            _logger.LogDebug("Started clearing Propagation Tree");
            timestamp = Stopwatch.GetTimestamp();
        }

        void LogSuccess()
        {
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            _logger.Log(
                elapsed < SlowActionDurationThreshold ? LogLevel.Debug : LogLevel.Information,
                "Finished clearing Propagation Tree in {ElapsedTime}",
                elapsed);
        }
    }

    private void CopyRemoteUpdates(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogStart(out var startTimestamp);

        foreach (var node in _updateTreeTraversal.ExcludeStartingNode().PreOrder(_remoteUpdateTree.Root, cancellationToken))
        {
            _propagationTree.Operations.Execute(new Operation<PropagationTreeNodeModel<TId>>(
                OperationType.Create,
                MappedFromRemote(node.Model)));
        }

        LogSuccess();

        return;

        void LogStart(out long timestamp)
        {
            _logger.LogDebug("Started copying remote updates");
            timestamp = Stopwatch.GetTimestamp();
        }

        void LogSuccess()
        {
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            _logger.Log(
                elapsed < SlowActionDurationThreshold ? LogLevel.Debug : LogLevel.Information,
                "Finished copying remote updates in {ElapsedTime}",
                elapsed);
        }
    }

    private void MergeLocalUpdates(CancellationToken cancellationToken)
    {
        LogStart(out var startTimestamp);

        DetachLocalUpdates(cancellationToken);

        try
        {
            RestoreAndMergeLocalUpdates(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Local updates are temporarily held outside the Local Update Tree, therefore the ones
            // that have not been merged yet would be lost if they were not restored back.
            RestoreDetachedLocalUpdates();

            throw;
        }

        LogSuccess();

        return;

        void LogStart(out long timestamp)
        {
            _logger.LogDebug("Started merging local updates");
            timestamp = Stopwatch.GetTimestamp();
        }

        void LogSuccess()
        {
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            _logger.Log(
                elapsed < SlowActionDurationThreshold ? LogLevel.Debug : LogLevel.Information,
                "Finished merging local updates in {ElapsedTime}",
                elapsed);
        }
    }

    /// <summary>
    /// Detaches local updates by moving contents of the Local Update Tree to in-memory cache.
    /// </summary>
    /// <remarks>
    /// If the method throws <see cref="OperationCanceledException"/>, the Local Update Tree retains all contents.
    /// </remarks>
    private void DetachLocalUpdates(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogStart(out var startTimestamp);

        CacheLocalUpdates(cancellationToken);

        ClearLocalUpdateTree();

        LogSuccess();

        return;

        void LogStart(out long timestamp)
        {
            _logger.LogDebug("Started detaching local updates");
            timestamp = Stopwatch.GetTimestamp();
        }

        void LogSuccess()
        {
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            _logger.Log(
                elapsed < SlowActionDurationThreshold ? LogLevel.Debug : LogLevel.Information,
                "Finished detaching local updates in {ElapsedTime}",
                elapsed);
        }
    }

    private void CacheLocalUpdates(CancellationToken cancellationToken)
    {
        LogStart(out var startTimestamp);

        _detachedLocalUpdates.Clear();

        // All except directory deletion.
        // Directory deletion involves processing child nodes, that might be moved to newly created parents,
        // which should be processed before processing any children.
        EnqueueLocalUpdates(FirstPassFilter, cancellationToken);

        // Directory deletion only
        EnqueueLocalUpdates(SecondPassFilter, cancellationToken);

        LogSuccess();

        return;

        void LogStart(out long timestamp)
        {
            _logger.LogDebug("Started caching local updates");
            timestamp = Stopwatch.GetTimestamp();
        }

        void LogSuccess()
        {
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            _logger.Log(
                elapsed < SlowActionDurationThreshold ? LogLevel.Debug : LogLevel.Information,
                "Finished caching local updates in {ElapsedTime}",
                elapsed);
        }
    }

    private void ClearLocalUpdateTree()
    {
        LogStart(out var startTimestamp);

        _localUpdateTree.Clear();

        LogSuccess();

        return;

        void LogStart(out long timestamp)
        {
            _logger.LogDebug("Started clearing Local Update Tree");
            timestamp = Stopwatch.GetTimestamp();
        }

        void LogSuccess()
        {
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            _logger.Log(
                elapsed < SlowActionDurationThreshold ? LogLevel.Debug : LogLevel.Information,
                "Finished clearing Local Update Tree in {ElapsedTime}",
                elapsed);
        }
    }

    private void EnqueueLocalUpdates(Func<UpdateTreeNode<TId>, bool> filter, CancellationToken cancellationToken)
    {
        foreach (var node in _updateTreeTraversal.ExcludeStartingNode()
                     .PreOrder(_localUpdateTree.Root, cancellationToken)
                     .Where(filter)
                     .Where(ContainsChange))
        {
            _detachedLocalUpdates.Enqueue(node.Model);
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

    private void RestoreAndMergeLocalUpdates(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        while (_detachedLocalUpdates.TryDequeue(out var nodeModel))
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

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// Adds previously detached local updates, that have not been merged, back to the Local Update Tree.
    /// </summary>
    private void RestoreDetachedLocalUpdates()
    {
        if (_detachedLocalUpdates.Count == 0)
        {
            return;
        }

        LogStart(out var startTimestamp);

        while (_detachedLocalUpdates.TryDequeue(out var nodeModel))
        {
            // The node might already exist if it has been re-created as a missing ancestor
            var node = _localUpdateTree.NodeByIdOrDefault(nodeModel.Id);

            // Ancestors might not exist if there is a pseudo conflict that was resolved during reconciliation
            _localUpdateTree.Operations.Execute(
                WithMissingAncestors(_equalizeLocalUpdateTreeOperationsFactory.Operations(node?.Model, nodeModel)));
        }

        LogSuccess();

        return;

        void LogStart(out long timestamp)
        {
            _logger.LogWarning("Started restoring {NumberOfNodes} not merged local updates", _detachedLocalUpdates.Count);
            timestamp = Stopwatch.GetTimestamp();
        }

        void LogSuccess()
        {
            _logger.LogWarning("Finished restoring not merged local updates in {ElapsedTime}", Stopwatch.GetElapsedTime(startTimestamp));
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

    private IEnumerable<Operation<UpdateTreeNodeModel<TId>>> WithMissingAncestors(
        IEnumerable<Operation<UpdateTreeNodeModel<TId>>> operations)
    {
        return _missingLocalUpdateTreeAncestorsFactory.WithMissingAncestors(operations);
    }

    private Task Schedule(Action origin)
    {
        return _syncScheduler.Schedule(origin);
    }
}
