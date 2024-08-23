using System;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared.IO;
using ProtonDrive.Shared.Telemetry;
using ProtonDrive.Sync.Adapter.Shared;
using ProtonDrive.Sync.Adapter.UpdateDetection;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.SyncActivity;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.OperationExecution;

internal sealed class NotifyingExecutionStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ExecutionStep<TId, TAltId> _executionStep;
    private readonly SyncActivity<TId> _syncActivity;
    private readonly IFileRevisionProvider<TId> _fileRevisionProvider;
    private readonly IErrorCounter _errorCounter;

    public NotifyingExecutionStep(
        ExecutionStep<TId, TAltId> executionStep,
        SyncActivity<TId> syncActivity,
        IFileRevisionProvider<TId> fileRevisionProvider,
        IErrorCounter errorCounter)
    {
        _executionStep = executionStep;
        _syncActivity = syncActivity;
        _fileRevisionProvider = fileRevisionProvider;
        _errorCounter = errorCounter;
    }

    public Task<NodeInfo<TAltId>> Execute(
        ExecutableOperation<TId> operation,
        NodeInfo<TAltId> nodeInfo,
        NodeInfo<TAltId>? destinationInfo,
        UpdateDetectionSwitch updateDetection,
        CancellationToken cancellationToken)
    {
        return operation.IsFileTransfer()
            ? ExecuteFileTransfer(operation, nodeInfo, destinationInfo, updateDetection, cancellationToken)
            : ExecuteSimpleOperation(operation, nodeInfo, destinationInfo, cancellationToken);
    }

    public void NotifySkipped(ExecutableOperation<TId> operation)
    {
        var syncActivity = operation.GetSyncActivityItem();

        _syncActivity.OnChanged(syncActivity, SyncActivityItemStatus.Skipped);
    }

    private async Task<NodeInfo<TAltId>> ExecuteFileTransfer(
        ExecutableOperation<TId> operation,
        NodeInfo<TAltId> nodeInfo,
        NodeInfo<TAltId>? destinationInfo,
        UpdateDetectionSwitch updateDetection,
        CancellationToken cancellationToken)
    {
        IRevision? sourceRevision;

        try
        {
            sourceRevision = await OpenFileForReading(operation.Model, cancellationToken).ConfigureAwait(false);
        }
        catch (FileRevisionProviderException ex)
        {
            // We report all failures to obtain source revision, even though some of them are transient
            var syncActivity = operation.GetSyncActivityItem(nodeInfo, destinationInfo);

            if (ex.ErrorCode is FileSystemErrorCode.Unknown)
            {
                _errorCounter.Add(ErrorScope.ItemOperation, ex);
            }

            switch (ex.ErrorCode)
            {
                case FileSystemErrorCode.SharingViolation:
                    _syncActivity.OnProgress(syncActivity, Progress.Zero);
                    _syncActivity.OnWarning(syncActivity, ex.ErrorCode);
                    break;

                case FileSystemErrorCode.LastWriteTimeTooRecent:
                    _syncActivity.OnChanged(syncActivity, SyncActivityItemStatus.Skipped, ex.ErrorCode);
                    break;

                default:
                    _syncActivity.OnProgress(syncActivity, Progress.Zero);
                    _syncActivity.OnFailed(syncActivity, ex.ErrorCode);
                    break;
            }

            throw;
        }

        await using (sourceRevision.ConfigureAwait(false))
        {
            var syncActivity = operation.GetSyncActivityItem(nodeInfo, destinationInfo, sourceRevision);

            return await WithSyncActivity(syncActivity, InternalExecute).ConfigureAwait(false);

            Task<NodeInfo<TAltId>> InternalExecute(Action<Progress> progressCallback)
            {
                return _executionStep.ExecuteFileTransferAsync(operation, nodeInfo, sourceRevision, updateDetection, progressCallback, cancellationToken);
            }
        }
    }

    private async Task<NodeInfo<TAltId>> ExecuteSimpleOperation(
        ExecutableOperation<TId> operation,
        NodeInfo<TAltId> nodeInfo,
        NodeInfo<TAltId>? destinationInfo,
        CancellationToken cancellationToken)
    {
        var syncActivity = operation.GetSyncActivityItem(nodeInfo, destinationInfo);

        return await WithSyncActivity(syncActivity, _ => InternalExecute()).ConfigureAwait(false);

        Task<NodeInfo<TAltId>> InternalExecute()
        {
            return _executionStep.ExecuteSimpleOperationAsync(operation, nodeInfo, destinationInfo, cancellationToken);
        }
    }

    private Task<IRevision> OpenFileForReading(AltIdentifiableFileSystemNodeModel<TId, TId> nodeModel, CancellationToken cancellationToken)
    {
        // ExecutableOperation.Model.AltId contains file Id on another adapter
        return _fileRevisionProvider.OpenFileForReadingAsync(nodeModel.AltId, nodeModel.ContentVersion, cancellationToken);
    }

    private async Task<TResult> WithSyncActivity<TResult>(
        SyncActivityItem<TId> syncActivityItem,
        Func<Action<Progress>, Task<TResult>> action)
    {
        try
        {
            _syncActivity.OnProgress(syncActivityItem, Progress.Zero);

            var result = await action(NotifyProgressChanged).ConfigureAwait(false);

            _syncActivity.OnSucceeded(syncActivityItem);

            return result;
        }
        catch (Exception ex)
        {
            var (errorCode, errorMessage) = ex.GetErrorInfo();

            if (errorCode is FileSystemErrorCode.Unknown)
            {
                _errorCounter.Add(ErrorScope.ItemOperation, ex);
            }

            switch (errorCode)
            {
                case FileSystemErrorCode.SharingViolation:
                    _syncActivity.OnWarning(syncActivityItem, errorCode, errorMessage);
                    break;

                case FileSystemErrorCode.Cancelled or FileSystemErrorCode.TransferAbortedDueToFileChange:
                    _syncActivity.OnCancelled(syncActivityItem, errorCode);
                    break;

                default:
                    _syncActivity.OnFailed(syncActivityItem, errorCode, errorMessage);
                    break;
            }

            throw;
        }

        void NotifyProgressChanged(Progress value)
        {
            _syncActivity.OnProgress(syncActivityItem, value);
        }
    }
}
