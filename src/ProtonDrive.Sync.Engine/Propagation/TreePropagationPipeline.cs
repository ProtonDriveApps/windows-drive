using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.Collections.Generic;
using ProtonDrive.Sync.Shared.ExecutionStatistics;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem.Traversal;

namespace ProtonDrive.Sync.Engine.Propagation;

internal class TreePropagationPipeline<TId>
    where TId : struct, IEquatable<TId>
{
    private readonly IScheduler _syncScheduler;
    private readonly PropagationTree<TId> _propagationTree;
    private readonly ILogger<TreePropagationPipeline<TId>> _logger;

    private readonly PseudoConflictPropagationPipeline<TId> _pseudoConflictPropagation;
    private readonly NodePropagationPipeline<TId> _remoteNodePropagation;
    private readonly NodePropagationPipeline<TId> _localNodePropagation;
    private readonly FileTransferPipeline<TId> _fileTransfer;

    private readonly PassiveTreeTraversal<PropagationTree<TId>, PropagationTreeNode<TId>, PropagationTreeNodeModel<TId>, TId>
        _propagationTreeTraversal;
    private readonly DeletedChildrenRestorationPipeline<TId> _remoteDeletedChildrenRestoration;
    private readonly UnchangedLeafDeletionOperationsFactory<TId> _unchangedLeafDeletionOperationsFactory;

    private bool _skipRemoteDirectoryDeletion;
    private bool _skipLocalDirectoryDeletion;
    private bool _skipNode;

    public TreePropagationPipeline(
        IScheduler syncScheduler,
        ISyncAdapter<TId> remoteAdapter,
        ISyncAdapter<TId> localAdapter,
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> remoteUpdateTree,
        UpdateTree<TId> localUpdateTree,
        PropagationTree<TId> propagationTree,
        IFileNameFactory<TId> tempUniqueNameFactory,
        ConcurrentExecutionStatistics executionStatistics,
        ILoggerFactory loggerFactory)
    {
        _syncScheduler = syncScheduler;
        _propagationTree = propagationTree;
        _logger = loggerFactory.CreateLogger<TreePropagationPipeline<TId>>();

        _pseudoConflictPropagation = new PseudoConflictPropagationPipeline<TId>(
            syncedTree,
            remoteUpdateTree,
            localUpdateTree,
            propagationTree);

        var propagatingNodes = new PropagatingNodes<TId>();

        _remoteNodePropagation = new NodePropagationPipeline<TId>(
            Replica.Remote,
            syncScheduler,
            remoteAdapter,
            syncedTree,
            remoteUpdateTree,
            localUpdateTree,
            propagationTree,
            tempUniqueNameFactory,
            propagatingNodes,
            executionStatistics);

        _localNodePropagation = new NodePropagationPipeline<TId>(
            Replica.Local,
            syncScheduler,
            localAdapter,
            syncedTree,
            localUpdateTree,
            remoteUpdateTree,
            propagationTree,
            tempUniqueNameFactory,
            propagatingNodes,
            executionStatistics);

        var leafsDeletionPipeline = new UnchangedLeafsDeletionPipeline<TId>(propagationTree, syncScheduler);

        _fileTransfer = new FileTransferPipeline<TId>(
            _localNodePropagation,
            _remoteNodePropagation,
            leafsDeletionPipeline,
            loggerFactory.CreateLogger<FileTransferPipeline<TId>>());

        _propagationTreeTraversal =
            new PassiveTreeTraversal<PropagationTree<TId>, PropagationTreeNode<TId>, PropagationTreeNodeModel<TId>,
                TId>();

        _remoteDeletedChildrenRestoration = new DeletedChildrenRestorationPipeline<TId>(Replica.Remote, syncedTree, remoteUpdateTree, propagationTree);
        _unchangedLeafDeletionOperationsFactory = new UnchangedLeafDeletionOperationsFactory<TId>();
    }

    /// <summary>
    /// Propagates updates recorded in the PropagationTree to the remote and local adapters,
    /// updates internal SyncEngine state to match the propagation results.
    /// </summary>
    /// <remarks>
    /// Some updates depend on other updates, that should be propagated first, therefore,
    /// might be skipped from propagating. Call this method again to propagate changes
    /// that were skipped.
    /// </remarks>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous propagation operation.</returns>
    public async Task Execute(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Started propagation");

        StartFileTransfer(cancellationToken);

        try
        {
            await ExecuteFirstPass(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await FinishFileTransferAsync().ConfigureAwait(false);
        }

        await ExecuteSecondPass(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Finished propagation");
    }

    private static UpdateStatus OptionallySkippingDirectoryDeletionFilter(
        PropagationTreeNodeModel<TId> model,
        UpdateStatus status,
        Func<PropagationTreeNodeModel<TId>, UpdateStatus, UpdateStatus> origin,
        bool skip)
    {
        var result = origin(model, status);

        return result.Contains(UpdateStatus.Deleted) && model.Type == NodeType.Directory && skip
            ? result.Minus(UpdateStatus.Deleted)
            : result;
    }

    private static UpdateStatus SkippingFileTransferFilter(
        PropagationTreeNodeModel<TId> model,
        UpdateStatus status)
    {
        return model.Type == NodeType.File && (status.Contains(UpdateStatus.Created) || status.Contains(UpdateStatus.Edited))
            ? status.Minus(UpdateStatus.Created | UpdateStatus.Edited)
            : status;
    }

    private static async Task WithSafeCancellation(Func<Task> origin)
    {
        try
        {
            await origin.Invoke().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
    }

    /// <summary>
    /// Propagates all changes except folder deletion
    /// </summary>
    private async Task ExecuteFirstPass(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await foreach (var node in FirstPassNodes(cancellationToken).ConfigureAwait(false))
        {
            await PropagateNode(node, FirstPassFilter, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Propagates folder deletion
    /// </summary>
    private async Task ExecuteSecondPass(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Directory deletion is skipped if any move is not yet propagated on the replica
        // as the directory marked for deletion might contain nodes to be moved.
        if (await ShouldSkipDirectoryDeletionAsync().ConfigureAwait(false))
        {
            return;
        }

        await foreach (var node in SecondPassNodes(cancellationToken).ConfigureAwait(false))
        {
            await PropagateNode(node, SecondPassFilter, cancellationToken).ConfigureAwait(false);
        }
    }

    private IAsyncEnumerable<PropagationTreeNode<TId>> FirstPassNodes(CancellationToken cancellationToken)
    {
        return new ScheduledEnumerable<PropagationTreeNode<TId>>(_syncScheduler, PropagationNodes(FirstPassFilter, cancellationToken));
    }

    private IAsyncEnumerable<PropagationTreeNode<TId>> SecondPassNodes(CancellationToken cancellationToken)
    {
        return new ScheduledEnumerable<PropagationTreeNode<TId>>(_syncScheduler, PropagationNodes(SecondPassFilter, cancellationToken));
    }

    private IEnumerable<PropagationTreeNode<TId>> PropagationNodes(Func<PropagationTreeNodeModel<TId>, UpdateStatus, UpdateStatus> filter, CancellationToken cancellationToken)
    {
        return _propagationTreeTraversal
            .ExcludeStartingNode()
            .DepthFirst(_propagationTree.Root, cancellationToken)
            .PostOrder(RemoveUnchangedPropagationTreeLeaf)
            .WherePreOrder()
            .SelectNode()
            .Where(n =>
                filter(n.Model, n.Model.LocalStatus) != UpdateStatus.Unchanged ||
                filter(n.Model, n.Model.RemoteStatus) != UpdateStatus.Unchanged);
    }

    private void StartFileTransfer(CancellationToken cancellationToken)
    {
        _fileTransfer.Start(cancellationToken);
    }

    private async Task FinishFileTransferAsync()
    {
        await WithSafeCancellation(() => _fileTransfer.FinishAsync()).ConfigureAwait(false);
    }

    private async Task PropagateNode(
        PropagationTreeNode<TId> node,
        Func<PropagationTreeNodeModel<TId>, UpdateStatus, UpdateStatus> filter,
        CancellationToken cancellationToken)
    {
        await ResolvePseudoConflicts(node, filter).ConfigureAwait(false);

        _skipNode = false;

        if (ShouldRestoreRemoteNode(node))
        {
            await PropagateRemoteNode(node, filter, cancellationToken).ConfigureAwait(false);
            await PropagateLocalNode(node, filter, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await PropagateLocalNode(node, filter, cancellationToken).ConfigureAwait(false);
            await PropagateRemoteNode(node, filter, cancellationToken).ConfigureAwait(false);
        }

        ScheduleFileTransfer(node);
    }

    private Task ResolvePseudoConflicts(
        PropagationTreeNode<TId> node,
        Func<PropagationTreeNodeModel<TId>, UpdateStatus, UpdateStatus> filter)
    {
        return Schedule(() => _pseudoConflictPropagation.Execute(
            node,
            (m, s) => OptionallySkippingDirectoryDeletionFilter(m, s, filter, _skipLocalDirectoryDeletion || _skipRemoteDirectoryDeletion)));
    }

    private bool ShouldRestoreRemoteNode(PropagationTreeNode<TId> node)
    {
        return _remoteDeletedChildrenRestoration.ShouldRestore(node);
    }

    private async Task PropagateLocalNode(
        PropagationTreeNode<TId> node,
        Func<PropagationTreeNodeModel<TId>, UpdateStatus, UpdateStatus> filter,
        CancellationToken cancellationToken)
    {
        if (_skipNode)
        {
            return;
        }

        UpdateStatus Filter(PropagationTreeNodeModel<TId> nodeModel, UpdateStatus status)
        {
            return SkippingFileTransferFilter(
                nodeModel,
                OptionallySkippingDirectoryDeletionFilter(nodeModel, status, filter, _skipLocalDirectoryDeletion));
        }

        var result = await _localNodePropagation.ExecuteAsync(node, Filter, cancellationToken).ConfigureAwait(false);

        AdjustTreeTraversal(result);
    }

    private async Task PropagateRemoteNode(
        PropagationTreeNode<TId> node,
        Func<PropagationTreeNodeModel<TId>, UpdateStatus, UpdateStatus> filter,
        CancellationToken cancellationToken)
    {
        if (_skipNode)
        {
            return;
        }

        UpdateStatus Filter(PropagationTreeNodeModel<TId> nodeModel, UpdateStatus status)
        {
            return SkippingFileTransferFilter(
                nodeModel,
                OptionallySkippingDirectoryDeletionFilter(nodeModel, status, filter, _skipRemoteDirectoryDeletion));
        }

        var result = await _remoteNodePropagation.ExecuteAsync(node, Filter, cancellationToken).ConfigureAwait(false);

        AdjustTreeTraversal(result);
    }

    private void ScheduleFileTransfer(PropagationTreeNode<TId> node)
    {
        if (_skipNode)
        {
            return;
        }

        _fileTransfer.ScheduleExecution(node, Replica.Remote);
        _fileTransfer.ScheduleExecution(node, Replica.Local);
    }

    private void AdjustTreeTraversal(ExecutionResultCode result)
    {
        if (result is ExecutionResultCode.Success)
        {
            return;
        }

        // If executing operation on one replica failed then execution
        // of operation for the same node on other replica is skipped.
        _skipNode = true;

        switch (result)
        {
            case ExecutionResultCode.Offline:
                _propagationTreeTraversal.SkipToRoot();
                return;

            case ExecutionResultCode.DirtyBranch:
                _propagationTreeTraversal.SkipToParent();
                return;

            default:
                _propagationTreeTraversal.SkipChildren();
                break;
        }
    }

    private UpdateStatus FirstPassFilter(PropagationTreeNodeModel<TId> model, UpdateStatus status)
    {
        // All changes except directory deletion
        return model.Type == NodeType.Directory
            ? status.Minus(UpdateStatus.Deleted)
            : status;
    }

    private UpdateStatus SecondPassFilter(PropagationTreeNodeModel<TId> model, UpdateStatus status)
    {
        // Directory deletion only
        return model.Type == NodeType.Directory
            ? status.Intersect(UpdateStatus.Deleted)
            : UpdateStatus.Unchanged;
    }

    private Task<bool> ShouldSkipDirectoryDeletionAsync()
    {
        return Schedule(ShouldSkipDirectoryDeletion);
    }

    private bool ShouldSkipDirectoryDeletion()
    {
        _skipRemoteDirectoryDeletion = false;
        _skipLocalDirectoryDeletion = false;

        var nodes = _propagationTreeTraversal
            .ExcludeStartingNode()
            .PreOrder(_propagationTree.Root);

        foreach (var node in nodes)
        {
            _skipRemoteDirectoryDeletion |= node.Model.RemoteStatus.Contains(UpdateStatus.Moved);
            _skipLocalDirectoryDeletion |= node.Model.LocalStatus.Contains(UpdateStatus.Moved);

            if (_skipRemoteDirectoryDeletion && _skipLocalDirectoryDeletion)
            {
                _logger.LogInformation("There are not propagated move operations left on both replicas. Skipping propagation of directory deletions if any");

                return true;
            }
        }

        if (_skipRemoteDirectoryDeletion)
        {
            _logger.LogInformation("There are not propagated move operations left on the Remote replica. Skipping propagation of directory deletions to the Remote replica if any");
        }

        if (_skipLocalDirectoryDeletion)
        {
            _logger.LogInformation("There are not propagated move operations left on the Local replica. Skipping propagation of directory deletions to the Local replica if any");
        }

        return false;
    }

    private void RemoveUnchangedPropagationTreeLeaf(PropagationTreeNode<TId> node)
    {
        _propagationTree.Operations.Execute(_unchangedLeafDeletionOperationsFactory.Operations(node));
    }

    private Task Schedule(Action origin)
    {
        return _syncScheduler.Schedule(origin);
    }

    private Task<T> Schedule<T>(Func<T> origin)
    {
        return _syncScheduler.Schedule(origin);
    }
}
