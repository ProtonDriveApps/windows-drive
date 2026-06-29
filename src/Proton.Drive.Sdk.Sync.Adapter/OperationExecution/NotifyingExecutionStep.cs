using Proton.Drive.Sdk.Sync.Adapter.Shared;
using Proton.Drive.Sdk.Sync.Adapter.UpdateDetection;
using Proton.Drive.Sdk.Sync.Shared.Adapters;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.SyncActivity;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.Operations;
using Proton.Drive.Shared.IO;
using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Adapter.OperationExecution;

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
        ISourceRevision sourceRevision;

        try
        {
            sourceRevision = await OpenFileForReading(operation.Model, cancellationToken).ConfigureAwait(false);

            await sourceRevision.CheckReadabilityAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IFileSystemErrorCodeProvider ex)
        {
            // We report all failures to obtain source revision, even though some of them are transient
            var syncActivity = operation.GetSyncActivityItem(nodeInfo, destinationInfo, stage: SyncActivityStage.Preparation);

            if (ex.ErrorCode is FileSystemErrorCode.Unknown)
            {
                _errorCounter.Add(ErrorScope.ItemOperation, exception);
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
            var syncActivity = operation.GetSyncActivityItem(nodeInfo, destinationInfo, sourceRevision, stage: SyncActivityStage.Preparation);

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

    private Task<ISourceRevision> OpenFileForReading(AltIdentifiableFileSystemNodeModel<TId, TId> nodeModel, CancellationToken cancellationToken)
    {
        // ExecutableOperation.Model.AltId contains file ID on another adapter
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
            if (syncActivityItem.Stage is not SyncActivityStage.Execution)
            {
                syncActivityItem = syncActivityItem with
                {
                    Stage = SyncActivityStage.Execution,
                };
            }

            _syncActivity.OnProgress(syncActivityItem, value);
        }
    }
}
