using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Adapter.OperationExecution;
using ProtonDrive.Sync.Adapter.Shared;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Adapter.Trees.StateMaintenance;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.Collections.Generic;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem.Traversal;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.SyncStateMaintenance;

/// <summary>
/// Updates the sync state of files and folders on the filesystem, dehydrates,
/// and hydrates them based on status flags in the State Maintenance Tree.
/// </summary>
internal sealed class FileSyncStateHandler<TId, TAltId> : IDisposable
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<FileSyncStateHandler<TId, TAltId>> _logger;
    private readonly IScheduler _executionScheduler;
    private readonly IScheduler _syncScheduler;
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly StateMaintenanceTree<TId> _stateMaintenanceTree;
    private readonly IReadOnlyDictionary<TId, RootInfo<TAltId>> _syncRoots;
    private readonly IFileSystemClient<TAltId> _fileSystemClient;
    private readonly FailureStep<TId, TAltId> _failureStep;

    private readonly ConcurrentQueue<TId> _syncedNodeIds = new();
    private readonly CoalescingAction _quickFileSyncStateHandling;
    private readonly SingleAction _repetitiveFileSyncStateHandling;
    private readonly PassiveTreeTraversal<StateMaintenanceTree<TId>, StateMaintenanceTreeNode<TId>, StateMaintenanceTreeNodeModel<TId>, TId>
        _stateMaintenanceTreeTraversal = new();

    private readonly ISchedulerTimer _timer;

    private volatile bool _stopping;

    public FileSyncStateHandler(
        ILogger<FileSyncStateHandler<TId, TAltId>> logger,
        AppConfig appConfig,
        IScheduler scheduler,
        IScheduler executionScheduler,
        IScheduler syncScheduler,
        AdapterTree<TId, TAltId> adapterTree,
        StateMaintenanceTree<TId> stateMaintenanceTree,
        IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots,
        IFileSystemClient<TAltId> fileSystemClient,
        FailureStep<TId, TAltId> failureStep)
    {
        _logger = logger;
        _executionScheduler = executionScheduler;
        _syncScheduler = syncScheduler;
        _adapterTree = adapterTree;
        _stateMaintenanceTree = stateMaintenanceTree;
        _syncRoots = syncRoots;
        _fileSystemClient = fileSystemClient;
        _failureStep = failureStep;

        _quickFileSyncStateHandling =
            new CoalescingAction(ct =>
                WithLoggedException(() =>
                    WithSafeCancellation(() =>
                        WithFaultyStateDetection(() =>
                            QuicklyHandleFileSyncStateAsync(ct)))));

        _repetitiveFileSyncStateHandling =
            new SingleAction(ct =>
                WithLoggedException(() =>
                    WithSafeCancellation(() =>
                        WithFaultyStateDetection(() =>
                            RepeatedlyHandleFileSyncStateAsync(ct)))));

        stateMaintenanceTree.Operations.Executed += OnStateMaintenanceTreeOperationExecuted;

        _timer = scheduler.CreateTimer();
        _timer.Interval = appConfig.FileSyncStateMaintenanceInterval;
        _timer.Tick += (_, _) => RunRepetitiveFileSyncStateHandling();
    }

    public void Start()
    {
        _timer.Start();
    }

    public Task StopAsync()
    {
        _stopping = true;
        _timer.Stop();
        _quickFileSyncStateHandling.Cancel();
        _repetitiveFileSyncStateHandling.Cancel();

        return WaitForCompletionAsync();
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    internal async Task WaitForCompletionAsync()
    {
        // Wait for all scheduled tasks to complete
        await _quickFileSyncStateHandling.CurrentTask.ConfigureAwait(false);
        await _repetitiveFileSyncStateHandling.CurrentTask.ConfigureAwait(false);
    }

    private async Task QuicklyHandleFileSyncStateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        while (_syncedNodeIds.TryDequeue(out var nodeId))
        {
            await ScheduleExecution(() => HandleFileSyncStateAsync(nodeId, isHydration: false, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RepeatedlyHandleFileSyncStateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await foreach (var nodeModel in CandidatesForStateUpdate(cancellationToken))
        {
            var result = nodeModel.IsHydrationPending()
                ? await HandleFileSyncStateAsync(nodeModel.Id, isHydration: true, cancellationToken).ConfigureAwait(false)
                : await ScheduleExecution(() => HandleFileSyncStateAsync(nodeModel.Id, isHydration: false, cancellationToken), cancellationToken).ConfigureAwait(false);

            AdjustTreeTraversal(result);
        }
    }

    private IAsyncEnumerable<StateMaintenanceTreeNodeModel<TId>> CandidatesForStateUpdate(CancellationToken cancellationToken)
    {
        return new ScheduledEnumerable<StateMaintenanceTreeNodeModel<TId>>(_syncScheduler, StateMaintenanceTreeNodes(cancellationToken));
    }

    private IEnumerable<StateMaintenanceTreeNodeModel<TId>> StateMaintenanceTreeNodes(CancellationToken cancellationToken)
    {
        return _stateMaintenanceTreeTraversal
            .ExcludeStartingNode()
            .DepthFirst(_stateMaintenanceTree.Root, cancellationToken)
            .WherePreOrder()
            .SelectNode()
            .Select(n => n.Model)
            .Where(m => m.IsCandidateForSyncStateUpdate());
    }

    private async Task<ExecutionResultCode> HandleFileSyncStateAsync(TId nodeId, bool isHydration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (nodeInfo, nodeModel, preparationResultCode) = await Schedule(() => Prepare(nodeId, isHydration), cancellationToken).ConfigureAwait(false);

        if (preparationResultCode != ExecutionResultCode.Success)
        {
            return preparationResultCode;
        }

        var (finalNodeInfo, exception) = await ExecuteAsync(nodeInfo!, nodeModel!.IsHydrationPending(), cancellationToken).ConfigureAwait(false);

        return finalNodeInfo != null
            ? Success(nodeModel!, finalNodeInfo)
            : await Schedule(() => Failure(nodeModel!, nodeInfo!, exception!), cancellationToken).ConfigureAwait(false);
    }

    private (NodeInfo<TAltId>? NodeInfo, AdapterTreeNodeModel<TId, TAltId>? NodeModel, ExecutionResultCode Code) Prepare(TId nodeId, bool isHydration)
    {
        var node = _adapterTree.NodeByIdOrDefault(nodeId);

        if (node == null)
        {
            _logger.LogDebug("Adapter Tree node with Id={Id} does not exist, skipping sync state update", nodeId);

            return (null, null, ExecutionResultCode.DirtyNode);
        }

        var syncRoot = _syncRoots[node.GetSyncRoot().Id];
        if (!syncRoot.IsEnabled)
        {
            _logger.LogDebug("Adapter Tree node with Id={Id} is in a disabled root with Id={RootId}", nodeId, syncRoot.Id);

            return (null, null, ExecutionResultCode.Offline);
        }

        if (node.IsNodeOrBranchDeleted())
        {
            _logger.LogDebug("Adapter Tree node with Id={Id} or branch is deleted, skipping sync state update", nodeId);

            return (null, null, ExecutionResultCode.DirtyNode);
        }

        if (!node.Model.IsSynced() || !node.Model.IsStateUpdatePending() || (isHydration != node.Model.IsHydrationPending()))
        {
            _logger.LogDebug("Adapter Tree node with Id={Id} has diverged, skipping sync state update", nodeId);

            return (null, null, ExecutionResultCode.DirtyNode);
        }

        var nodeInfo = node.ToNodeInfo(_syncRoots);

        if (nodeInfo.IsDirectory())
        {
            // Not matching last write time should not prevent updating sync state of the folder,
            // because last write time value is not synced to Proton Drive for folders.
            nodeInfo = nodeInfo.WithLastWriteTimeUtc(default);
        }

        return (nodeInfo, node.Model, ExecutionResultCode.Success);
    }

    private async Task<(NodeInfo<TAltId>? NodeInfo, Exception? Exception)> ExecuteAsync(
        NodeInfo<TAltId> nodeInfo,
        bool isHydrationPending,
        CancellationToken cancellationToken)
    {
        try
        {
            if (isHydrationPending)
            {
                await _fileSystemClient.HydrateFileAsync(nodeInfo, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _fileSystemClient.SetInSyncState(nodeInfo);
            }

            return (nodeInfo, null);
        }
        catch (FileSystemClientException exception)
        {
            return (null, exception);
        }
    }

    private ExecutionResultCode Success(AdapterTreeNodeModel<TId, TAltId> model, NodeInfo<TAltId> nodeInfo)
    {
        var type = nodeInfo.IsDirectory() ? NodeType.Directory : NodeType.File;
        var pathToLog = _logger.GetSensitiveValueForLogging(nodeInfo.Path);

        if (model.IsHydrationPending())
        {
            _logger.LogInformation(
                "Hydrated {Type} \"{Path}\" with Id=\"{Root}\"/{Id} {ExternalId}, ContentVersion={ContentVersion}",
                type,
                pathToLog,
                nodeInfo.Root?.Id,
                model.Id,
                nodeInfo.GetCompoundId(),
                model.ContentVersion);
        }
        else
        {
            _logger.LogInformation(
                "Updated state of {Type} \"{Path}\" with Id=\"{Root}\"/{Id} {ExternalId}, ContentVersion={ContentVersion}",
                type,
                pathToLog,
                nodeInfo.Root?.Id,
                model.Id,
                nodeInfo.GetCompoundId(),
                model.ContentVersion);
        }

        return ExecutionResultCode.Success;
    }

    private ExecutionResultCode Failure(FileSystemNodeModel<TId> model, NodeInfo<TAltId> nodeInfo, Exception exception)
    {
        var type = nodeInfo.IsDirectory() ? NodeType.Directory : NodeType.File;
        var pathToLog = _logger.GetSensitiveValueForLogging(nodeInfo.Path);

        _logger.LogWarning(
            "Updating state of {Type} \"{Path}\" with Id=\"{Root}\"/{Id} {ExternalId}, ContentVersion={ContentVersion} failed: {ErrorMessage}",
            type,
            pathToLog,
            nodeInfo.Root?.Id,
            model.Id,
            nodeInfo.GetCompoundId(),
            model.ContentVersion,
            exception.CombinedMessage());

        return _failureStep.Execute(exception, nodeInfo, destinationInfo: null);
    }

    private void AdjustTreeTraversal(ExecutionResultCode result)
    {
        switch (result)
        {
            case ExecutionResultCode.Success:
                return;

            case ExecutionResultCode.Offline:
                _stateMaintenanceTreeTraversal.SkipToRoot();
                return;

            case ExecutionResultCode.DirtyBranch:
                _stateMaintenanceTreeTraversal.SkipToParent();
                return;

            default:
                _stateMaintenanceTreeTraversal.SkipChildren();
                break;
        }
    }

    private void OnStateMaintenanceTreeOperationExecuted(object? sender, FileSystemTreeOperationExecutedEventArgs<StateMaintenanceTreeNodeModel<TId>, TId> eventArgs)
    {
        if (eventArgs.Type is OperationType.Delete)
        {
            return;
        }

        // All operations except Delete have a non-null NewModel value
        var newModel = eventArgs.NewModel ?? throw new InvalidOperationException();

        if (!newModel.IsCandidateForSyncStateUpdate())
        {
            // The node is not synced or does not require updating sync state
            return;
        }

        if (eventArgs.OldModel is { } oldModel && oldModel.IsCandidateForSyncStateUpdate())
        {
            // The node already was a candidate for updating file sync state before executing current operation on Adapter Tree
            return;
        }

        if (newModel.IsHydrationPending())
        {
            // Hydration is not handled by quick file sync state handling
            return;
        }

        _syncedNodeIds.Enqueue(newModel.Id);

        RunQuickFileSyncStateHandling();
    }

    private void RunQuickFileSyncStateHandling()
    {
        if (_stopping)
        {
            return;
        }

        _quickFileSyncStateHandling.Run();

        if (_stopping)
        {
            _quickFileSyncStateHandling.Cancel();
        }
    }

    private void RunRepetitiveFileSyncStateHandling()
    {
        if (_stopping)
        {
            return;
        }

        _repetitiveFileSyncStateHandling.RunAsync();

        if (_stopping)
        {
            _repetitiveFileSyncStateHandling.Cancel();
        }
    }

    private void Fault()
    {
        _logger.LogWarning("{ComponentName} is stopping", nameof(FileSyncStateHandler<TId, TAltId>));

        _ = StopAsync();
    }

    private async Task WithFaultyStateDetection(Func<Task> origin)
    {
        try
        {
            await origin.Invoke().ConfigureAwait(false);
        }
        catch (FaultyStateException ex)
        {
            // Ignore
            _logger.LogWarning("{ComponentName} operation failed: {ErrorMessage}", nameof(FileSyncStateHandler<TId, TAltId>), ex.Message);

            Fault();
        }
    }

    private Task WithSafeCancellation(Func<Task> origin)
    {
        return _logger.WithSafeCancellation(origin, nameof(FileSyncStateHandler<TId, TAltId>));
    }

    private Task WithLoggedException(Func<Task> origin)
    {
        return _logger.WithLoggedException(origin, $"{nameof(FileSyncStateHandler<TId, TAltId>)} operation failed", includeStackTrace: true);
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    private Task<TResult> ScheduleExecution<TResult>(Func<Task<TResult>> origin, CancellationToken cancellationToken)
    {
        return _executionScheduler.Schedule(origin, cancellationToken);
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    private Task<TResult> Schedule<TResult>(Func<TResult> origin, CancellationToken cancellationToken)
    {
        return _syncScheduler.Schedule(origin, cancellationToken);
    }
}
