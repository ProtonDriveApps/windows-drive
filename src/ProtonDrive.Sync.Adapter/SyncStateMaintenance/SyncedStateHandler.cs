using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Trees.Changes;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.SyncStateMaintenance;

/// <summary>
/// Maintains the value of <see cref="AdapterNodeStatus.Synced"/> flag of Adapter Tree nodes.
/// </summary>
internal sealed class SyncedStateHandler<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<SyncedStateHandler<TId, TAltId>> _logger;
    private readonly IScheduler _syncScheduler;
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly IReceivedTreeChanges<TId> _syncedUpdates;

    private readonly IEqualityComparer<FileSystemNodeModel<TId>> _linkEqualityComparer = new FileSystemNodeModelLinkEqualityComparer<TId>();
    private readonly IEqualityComparer<FileSystemNodeModel<TId>> _attributesEqualityComparer = new FileSystemNodeModelAttributesEqualityComparer<TId>();
    private readonly IEqualityComparer<AdapterTreeNodeModel<TId, TAltId>> _metadataEqualityComparer = new AdapterTreeNodeModelMetadataEqualityComparer<TId, TAltId>();
    private readonly CoalescingAction _updatesHandling;

    private volatile bool _stopping;
    private bool _updatingAdapterTree;

    public SyncedStateHandler(
        ILogger<SyncedStateHandler<TId, TAltId>> logger,
        IScheduler syncScheduler,
        AdapterTree<TId, TAltId> adapterTree,
        IReceivedTreeChanges<TId> syncedUpdates)
    {
        _logger = logger;
        _syncScheduler = syncScheduler;
        _adapterTree = adapterTree;
        _syncedUpdates = syncedUpdates;

        _updatesHandling =
            new CoalescingAction(ct =>
                WithLoggedException(() =>
                    WithSafeCancellation(() =>
                        WithFaultyStateDetection(() =>
                            HandleSyncedUpdatesAsync(ct)))));

        _syncedUpdates.Added += OnSyncedUpdatesAdded;
        adapterTree.Operations.Executing += OnAdapterTreeOperationExecuting;
    }

    public Task StopAsync()
    {
        _stopping = true;
        _updatesHandling.Cancel();

        return WaitForCompletionAsync();
    }

    internal Task WaitForCompletionAsync()
    {
        // Wait for all scheduled tasks to complete
        return _updatesHandling.CurrentTask;
    }

    private Task HandleSyncedUpdatesAsync(CancellationToken cancellationToken)
    {
        return Schedule(() => HandleSyncedUpdates(cancellationToken), cancellationToken);
    }

    private void HandleSyncedUpdates(CancellationToken cancellationToken)
    {
        foreach (var syncedTreeChange in _syncedUpdates)
        {
            HandleSyncedStateChange(syncedTreeChange.Operation);

            _syncedUpdates.AcknowledgeConsumed(syncedTreeChange);

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private void HandleSyncedStateChange(Operation<FileSystemNodeModel<TId>> operation)
    {
        if (operation.Type is OperationType.Delete)
        {
            HandleSyncedStateDeletion(operation.Model);
        }
        else
        {
            HandleSyncedStateUpdate(operation.Model);
        }
    }

    private void HandleSyncedStateDeletion(FileSystemNodeModel<TId> syncedState)
    {
        var node = _adapterTree.NodeByIdOrDefault(syncedState.Id);
        if (node is null)
        {
            return;
        }

        var model = node.Model.Copy().WithSyncedFlag(false);

        UpdateNode(node, model);
    }

    private void HandleSyncedStateUpdate(FileSystemNodeModel<TId> syncedState)
    {
        var node = _adapterTree.NodeByIdOrDefault(syncedState.Id);
        if (node is null)
        {
            return;
        }

        // The synced state is kept only for Adapter Tree nodes that have diverged from it
        var isInSync = SyncedStateEquals(syncedState, node.Model);

        var model = node.Model.Copy().WithSyncedFlag(isInSync);

        UpdateNode(node, model);
    }

    private void OnSyncedUpdatesAdded(object? sender, EventArgs e)
    {
        if (_stopping)
        {
            return;
        }

        _updatesHandling.Run();

        if (_stopping)
        {
            _updatesHandling.Cancel();
        }
    }

    private void UpdateNode(AdapterTreeNode<TId, TAltId> node, AdapterTreeNodeModel<TId, TAltId> incomingNodeModel)
    {
        if (_metadataEqualityComparer.Equals(node.Model, incomingNodeModel))
        {
            return;
        }

        LogSyncedStateUpdate(incomingNodeModel);

        var operation = new Operation<AdapterTreeNodeModel<TId, TAltId>>(
            OperationType.Update,
            incomingNodeModel);

        ExecuteOnAdapterTree(operation);
    }

    private void ExecuteOnAdapterTree(Operation<AdapterTreeNodeModel<TId, TAltId>> operation)
    {
        try
        {
            _updatingAdapterTree = true;

            _adapterTree.Operations.Execute(operation);
        }
        finally
        {
            _updatingAdapterTree = false;
        }
    }

    private void OnAdapterTreeOperationExecuting(object? sender, FileSystemTreeOperationExecutingEventArgs<AdapterTreeNodeModel<TId, TAltId>, TId> eventArgs)
    {
        if (_updatingAdapterTree)
        {
            return;
        }

        if (eventArgs.Type is OperationType.Delete or OperationType.Create)
        {
            // Deletion does not require processing, for created node we leave provided values
            return;
        }

        eventArgs.NewModel = HandleAdapterTreeNodeUpdating(eventArgs.OldModel!, eventArgs.NewModel!);
    }

    private AdapterTreeNodeModel<TId, TAltId> HandleAdapterTreeNodeUpdating(AdapterTreeNodeModel<TId, TAltId> oldNodeModel, AdapterTreeNodeModel<TId, TAltId> newNodeModel)
    {
        var stateChanging = !SyncedStateEquals(oldNodeModel, newNodeModel);

        if (!stateChanging)
        {
            // The node state does not change, then the synced state should not change either
            if (oldNodeModel.IsSynced() == newNodeModel.IsSynced())
            {
                return newNodeModel;
            }

            return newNodeModel.Copy().WithSyncedFlag(oldNodeModel.IsSynced());
        }

        if (!newNodeModel.IsSynced())
        {
            return newNodeModel;
        }

        // The node is diverging from synced state
        var divergedNodeModel = newNodeModel.Copy().WithSyncedFlag(false);

        LogSyncedStateUpdate(divergedNodeModel);

        return divergedNodeModel;
    }

    private bool SyncedStateEquals(FileSystemNodeModel<TId> nodeModelA, FileSystemNodeModel<TId> nodeModelB)
    {
        return _linkEqualityComparer.Equals(nodeModelA, nodeModelB) &&
               _attributesEqualityComparer.Equals(nodeModelA, nodeModelB);
    }

    private void LogSyncedStateUpdate(AdapterTreeNodeModel<TId, TAltId> nodeModel)
    {
        _logger.LogDebug(
            "Updating Adapter Tree node with Id={Id} {AltId}, ParentId={ParentId}, \"{Name}\", ContentVersion={ContentVersion}, to synced status flag(s)=({Flags})",
            nodeModel.Id,
            nodeModel.AltId,
            nodeModel.ParentId,
            nodeModel.Name,
            nodeModel.ContentVersion,
            nodeModel.Status & AdapterNodeStatus.Synced);
    }

    private void Fault()
    {
        _logger.LogWarning("{ComponentName} is stopping", nameof(SyncedStateHandler<TId, TAltId>));

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
            _logger.LogWarning("{ComponentName} operation failed: {ErrorMessage}", nameof(SyncedStateHandler<TId, TAltId>), ex.Message);

            Fault();
        }
    }

    private Task WithSafeCancellation(Func<Task> origin)
    {
        return _logger.WithSafeCancellation(origin, nameof(SyncedStateHandler<TId, TAltId>));
    }

    private Task WithLoggedException(Func<Task> origin)
    {
        return _logger.WithLoggedException(origin, $"{nameof(SyncedStateHandler<TId, TAltId>)} operation failed", includeStackTrace: true);
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    private Task Schedule(Action origin, CancellationToken cancellationToken)
    {
        return _syncScheduler.Schedule(origin, cancellationToken);
    }
}
