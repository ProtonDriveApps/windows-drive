using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.Propagation;

internal sealed class FileTransferPipeline<TId>
    where TId : IEquatable<TId>
{
    private const int MaxNumberOfConcurrentFileTransfers = 4;

    private readonly INodePropagationPipeline<TId> _localNodePropagation;
    private readonly INodePropagationPipeline<TId> _remoteNodePropagation;
    private readonly UnchangedLeafsDeletionPipeline<TId> _leafsDeletion;
    private readonly ILogger<FileTransferPipeline<TId>> _logger;

    private readonly ConcurrentQueue<FileTransfer<TId>> _scheduledTransfers = new();
    private readonly ConcurrentQueue<FileTransfer<TId>> _skippedTransfers = new();
    private readonly List<ExecutingTransfer> _executingTransfers = new(MaxNumberOfConcurrentFileTransfers);
    private readonly HashSet<TId> _dirtyFolders = new();
    private readonly IScheduler _scheduler = new SerialScheduler();

    private CancellationToken _cancellationToken = new(canceled: true);
    private TaskCompletionSource _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Status _status;

    public FileTransferPipeline(
        INodePropagationPipeline<TId> localNodePropagation,
        INodePropagationPipeline<TId> remoteNodePropagation,
        UnchangedLeafsDeletionPipeline<TId> leafsDeletion,
        ILogger<FileTransferPipeline<TId>> logger)
    {
        _localNodePropagation = localNodePropagation;
        _remoteNodePropagation = remoteNodePropagation;
        _leafsDeletion = leafsDeletion;
        _logger = logger;
    }

    private enum Status
    {
        NotStarted,
        Started,
        Finishing,
    }

    public void ScheduleExecution(PropagationTreeNode<TId> node, Replica replica)
    {
        var nodeModel = node.Model;
        if (FileTransferFilter(nodeModel, nodeModel.OwnStatus(replica)) == UpdateStatus.Unchanged)
        {
            return;
        }

        _scheduledTransfers.Enqueue(new FileTransfer<TId>(replica, node, nodeModel));
        _logger.LogDebug("Scheduled file transfer of \"{Name}\"/{ParentId}/{Id}", nodeModel.Name, nodeModel.ParentId, nodeModel.Id);
        ScheduleInternally(HandleExecution);
    }

    /// <summary>
    /// Starts the pipeline. The pipeline supports starting and finishing it multiple times.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="InvalidOperationException">The pipeline has already been started or has not been finished.</exception>
    public void Start(CancellationToken cancellationToken)
    {
        if (_status is not Status.NotStarted)
        {
            throw new InvalidOperationException($"File Transfer Pipeline status is {_status}");
        }

        _cancellationToken = cancellationToken;
        _completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _dirtyFolders.Clear();
        _status = Status.Started;
    }

    /// <summary>
    /// Completes scheduled file transfers.
    /// </summary>
    /// <returns>The <see cref="Task"/> that completes when all scheduled file transfers complete.</returns>
    /// <exception cref="InvalidOperationException">The pipeline has not been started or has already been finished.</exception>
    public Task FinishAsync()
    {
        if (_status is not Status.Started)
        {
            throw new InvalidOperationException($"File Transfer Pipeline status is {_status}");
        }

        _status = Status.Finishing;
        ScheduleInternally(HandleExecution);

        return _completionSource.Task;
    }

    private static UpdateStatus FileTransferFilter(PropagationTreeNodeModel<TId> nodeModel, UpdateStatus status)
    {
        return status.Intersect(nodeModel.Type is NodeType.File && (status.Contains(UpdateStatus.Created) || status.Contains(UpdateStatus.Edited))
            ? UpdateStatus.Created | UpdateStatus.Edited | UpdateStatus.Restore
            : UpdateStatus.Unchanged);
    }

    private void HandleExecution()
    {
        RemoveCompletedTransfers();

        HandleCancellation();

        StartScheduledTransfers();

        HandleFinishing();
    }

    private void RemoveCompletedTransfers()
    {
        for (var i = _executingTransfers.Count - 1; i >= 0; i--)
        {
            var (transfer, task) = _executingTransfers[i];

            if (!task.IsCompleted)
            {
                continue;
            }

            if (task is { IsCompletedSuccessfully: true, Result: ExecutionResultCode.DirtyBranch })
            {
                _dirtyFolders.Add(transfer.NodeModel.ParentId);
            }

            if (task is { IsCompletedSuccessfully: true, Result: ExecutionResultCode.SkippedInternally } && transfer.NumberOfRetries < 1)
            {
                transfer = transfer with
                {
                    NumberOfRetries = transfer.NumberOfRetries + 1,
                };

                _skippedTransfers.Enqueue(transfer);
                _logger.LogDebug("Scheduled for later file transfer of \"{Name}\"/{ParentId}/{Id}", transfer.NodeModel.Name, transfer.NodeModel.ParentId, transfer.NodeModel.Id);
            }

            _executingTransfers.RemoveAt(i);
        }
    }

    private void StartScheduledTransfers()
    {
        while (_executingTransfers.Count < MaxNumberOfConcurrentFileTransfers && TryGetNextScheduledTransfer(out var transfer))
        {
            if (_dirtyFolders.Contains(transfer.NodeModel.ParentId))
            {
                // Branch is dirty, skipping file transfer execution
                continue;
            }

            _logger.LogDebug("Scheduling start of file transfer of \"{Name}\"/{ParentId}/{Id}", transfer.NodeModel.Name, transfer.NodeModel.ParentId, transfer.NodeModel.Id);
            Execute(transfer);
        }
    }

    private bool TryGetNextScheduledTransfer([NotNullWhen(true)] out FileTransfer<TId>? item)
    {
        item = default;

        if (_cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return _scheduledTransfers.TryDequeue(out item) ||
               (_status is Status.Finishing && _skippedTransfers.TryDequeue(out item));
    }

    private void HandleCancellation()
    {
        if (!_cancellationToken.IsCancellationRequested)
        {
            return;
        }

        _scheduledTransfers.Clear();
        _skippedTransfers.Clear();
    }

    private void HandleFinishing()
    {
        if (_status is not Status.Finishing)
        {
            return;
        }

        if (!_scheduledTransfers.IsEmpty ||
            !_skippedTransfers.IsEmpty ||
            _executingTransfers.Count > 0)
        {
            return;
        }

        if (_cancellationToken.IsCancellationRequested)
        {
            _completionSource.SetCanceled(_cancellationToken);
        }
        else
        {
            _completionSource.SetResult();
        }

        _status = Status.NotStarted;
    }

    private void Execute(FileTransfer<TId> transfer)
    {
        var task = Task.Run(() => WithLoggedException(() => WithSafeCancellation(() => ExecuteAsync(transfer, _cancellationToken))), _cancellationToken);

        _executingTransfers.Add(new ExecutingTransfer(transfer, task));

        task.ContinueWith(
            _ => ScheduleInternally(HandleExecution),
            TaskContinuationOptions.ExecuteSynchronously);
    }

    private async Task<ExecutionResultCode> ExecuteAsync(FileTransfer<TId> transfer, CancellationToken cancellationToken)
    {
        var nodeModel = transfer.Node.Model;
        _logger.LogDebug("Starting execution of file transfer of \"{Name}\"/{ParentId}/{Id}", nodeModel.Name, nodeModel.ParentId, nodeModel.Id);

        var nodePropagation = transfer.Replica == Replica.Local ? _localNodePropagation : _remoteNodePropagation;

        var resultCode = await nodePropagation.ExecuteAsync(transfer.Node, FileTransferFilter, cancellationToken).ConfigureAwait(false);

        await _leafsDeletion.ExecuteAsync(transfer.Node).ConfigureAwait(false);

        _logger.LogDebug("Finished execution of file transfer of \"{Name}\"/{ParentId}/{Id} with result code {ResultCode}", nodeModel.Name, nodeModel.ParentId, nodeModel.Id, resultCode);

        return resultCode;
    }

    private Task<T> WithLoggedException<T>(Func<Task<T>> origin)
    {
        return _logger.WithLoggedException(origin, "Failed to transfer file", includeStackTrace: true);
    }

    private async Task<ExecutionResultCode> WithSafeCancellation(Func<Task<ExecutionResultCode>> origin)
    {
        try
        {
            return await origin.Invoke().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Ignore
            _logger.LogDebug($"{nameof(FileTransferPipeline<TId>)} operation was cancelled");

            return ExecutionResultCode.Cancelled;
        }
        catch (FaultyStateException)
        {
            // Ignore
            _logger.LogWarning($"{nameof(FileTransferPipeline<TId>)} operation failed due to faulty state");

            return ExecutionResultCode.Cancelled;
        }
    }

    private Task ScheduleInternally(Action origin) => _scheduler.Schedule(origin);

    private sealed record ExecutingTransfer(FileTransfer<TId> Transfer, Task<ExecutionResultCode> Task);
}
