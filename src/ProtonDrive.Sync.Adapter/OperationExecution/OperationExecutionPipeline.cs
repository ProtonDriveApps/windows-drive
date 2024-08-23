using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Adapter.NodeCopying;
using ProtonDrive.Sync.Adapter.Shared;
using ProtonDrive.Sync.Adapter.UpdateDetection;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.OperationExecution;

internal sealed class OperationExecutionPipeline<TId, TAltId> : IOperationExecutor<TId>
    where TId : struct, IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly IScheduler _executionScheduler;
    private readonly ITransactedScheduler _syncScheduler;
    private readonly ICopiedNodes<TId, TAltId> _copiedNodes;
    private readonly UpdateDetectionSequencer _updateDetectionSequencer;
    private readonly IFileSystemAccessRateLimiter<TId> _accessRateLimiter;
    private readonly PreconditionsValidationStep<TId, TAltId> _preconditionsValidation;
    private readonly PreparationStep<TId, TAltId> _preparation;
    private readonly NotifyingExecutionStep<TId, TAltId> _execution;
    private readonly SuccessStep<TId, TAltId> _success;
    private readonly FailureStep<TId, TAltId> _failure;
    private readonly NameConflictStep<TId, TAltId> _nameConflictStep;
    private readonly LoggingStep<TId, TAltId> _logging;

    private readonly OperationLogging<TId> _startedLogging;
    private readonly OperationLogging<TId> _finishedLogging;

    public OperationExecutionPipeline(
        ILogger<OperationExecutionPipeline<TId, TAltId>> logger,
        IScheduler executionScheduler,
        ITransactedScheduler syncScheduler,
        ICopiedNodes<TId, TAltId> copiedNodes,
        UpdateDetectionSequencer updateDetectionSequencer,
        IFileSystemAccessRateLimiter<TId> accessRateLimiter,
        PreconditionsValidationStep<TId, TAltId> preconditionsValidation,
        PreparationStep<TId, TAltId> preparation,
        NotifyingExecutionStep<TId, TAltId> execution,
        SuccessStep<TId, TAltId> success,
        FailureStep<TId, TAltId> failure,
        NameConflictStep<TId, TAltId> nameConflictStep,
        LoggingStep<TId, TAltId> logging)
    {
        _executionScheduler = executionScheduler;
        _syncScheduler = syncScheduler;
        _copiedNodes = copiedNodes;
        _updateDetectionSequencer = updateDetectionSequencer;
        _accessRateLimiter = accessRateLimiter;
        _preconditionsValidation = preconditionsValidation;
        _preparation = preparation;
        _execution = execution;
        _success = success;
        _failure = failure;
        _nameConflictStep = nameConflictStep;
        _logging = logging;

        _startedLogging = new OperationLogging<TId>("Started executing operation", logger);
        _finishedLogging = new OperationLogging<TId>("Finished executing operation", logger);
    }

    public Task<ExecutionResult<TId>> ExecuteAsync(
        ExecutableOperation<TId> operation,
        CancellationToken cancellationToken)
    {
        return operation.IsFileTransfer()

            // File transfer is not scheduled on the execution scheduler so that it can run
            // in parallel with update detection and any other operation, including other file transfers.
            ? ExecuteInternal(operation, cancellationToken)

            // Non file transfer operation is scheduled on the execution scheduler so that it is prevented
            // from running in parallel with update detection and other non file transfer operations.
            : ScheduleExecution(() => ExecuteInternal(operation, cancellationToken));
    }

    private async Task<ExecutionResult<TId>> ExecuteInternal(
        ExecutableOperation<TId> operation,
        CancellationToken cancellationToken)
    {
        LogStartedOperationExecution(operation);

        var (preconditionsResult, nodeInfo, destinationInfo) = await Schedule(() => Prepare(operation), cancellationToken).ConfigureAwait(false);

        if (preconditionsResult != null)
        {
            if (IsDeletingCopiedNodes(operation, preconditionsResult))
            {
                await ScheduleAndCommit(() => HandleCopiedNodesDeletionAttempt(operation), cancellationToken).ConfigureAwait(false);
            }

            return preconditionsResult;
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var updateDetection = new UpdateDetectionSwitch(_updateDetectionSequencer);

        var (finalNodeInfo, exception) = await Execute(operation, nodeInfo!, destinationInfo, updateDetection, cancellationToken).ConfigureAwait(false);

        var result = await ScheduleAndCommit(
                () => finalNodeInfo != null
                    ? HandleSuccess(operation, nodeInfo!, destinationInfo, finalNodeInfo)
                    : HandleFailure(operation, nodeInfo!, destinationInfo, exception!),
                cancellationToken)
            .ConfigureAwait(false);

        LogFinishedOperationExecution(operation);

        return result;
    }

    private (ExecutionResult<TId>? Result, NodeInfo<TAltId>? NodeInfo, NodeInfo<TAltId>? DestinationInfo) Prepare(
        ExecutableOperation<TId> operation)
    {
        var result = _preconditionsValidation.Execute(operation);
        if (result != null)
        {
            if (result.Code is ExecutionResultCode.NameConflict
                or ExecutionResultCode.DirtyNode
                or ExecutionResultCode.DirtyBranch
                or ExecutionResultCode.DirtyDestination
                or ExecutionResultCode.Error)
            {
                _execution.NotifySkipped(operation);
            }

            return (result, null, null);
        }

        var (nodeInfo, destinationInfo) = _preparation.Execute(operation);

        if (_accessRateLimiter.CanExecute(operation, out var resultCode))
        {
            return (null, nodeInfo, destinationInfo);
        }

        _execution.NotifySkipped(operation);

        return (ExecutionResult<TId>.Failure(resultCode), null, null);
    }

    private async Task<(NodeInfo<TAltId>? Value, Exception? Exception)> Execute(
        ExecutableOperation<TId> operation,
        NodeInfo<TAltId> nodeInfo,
        NodeInfo<TAltId>? destinationInfo,
        UpdateDetectionSwitch updateDetection,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await _execution.Execute(operation, nodeInfo, destinationInfo, updateDetection, cancellationToken).ConfigureAwait(false), null);
        }
        catch (Exception e) when (e is FileSystemClientException or FileRevisionProviderException)
        {
            _logging.LogFailure(operation, nodeInfo, destinationInfo, e);

            return (null, e);
        }
        catch (OperationCanceledException e)
        {
            return (null, e);
        }
    }

    private ExecutionResult<TId> HandleSuccess(
        ExecutableOperation<TId> operation,
        NodeInfo<TAltId> nodeInfo,
        NodeInfo<TAltId>? destinationInfo,
        NodeInfo<TAltId> finalNodeInfo)
    {
        _logging.LogSuccess(operation, operation.Type is OperationType.Create ? finalNodeInfo : nodeInfo, destinationInfo);

        _accessRateLimiter.HandleSuccess(operation);

        if (_preconditionsValidation.ExecuteBeforeApplyingResult(operation) is { } result)
        {
            return result;
        }

        _success.Execute(operation, finalNodeInfo);

        return ExecutionResult<TId>.Success();
    }

    private ExecutionResult<TId> HandleFailure(
        ExecutableOperation<TId> operation,
        NodeInfo<TAltId> nodeInfo,
        NodeInfo<TAltId>? destinationInfo,
        Exception exception)
    {
        if (_nameConflictStep.Execute(operation, exception) is { } nameConflictResult)
        {
            _accessRateLimiter.HandleFailure(operation, nameConflictResult.Code, FileSystemErrorCode.DuplicateName);

            return nameConflictResult;
        }

        var resultCode = _failure.Execute(exception, nodeInfo, destinationInfo);

        var errorCode = exception is FileSystemClientException ex ? ex.ErrorCode : FileSystemErrorCode.Unknown;

        _accessRateLimiter.HandleFailure(operation, resultCode, errorCode);

        return ExecutionResult<TId>.Failure(resultCode);
    }

    private bool IsDeletingCopiedNodes(ExecutableOperation<TId> operation, ExecutionResult<TId> preconditionsValidationResult)
    {
        return
            operation.Type is OperationType.Delete &&
            preconditionsValidationResult.Code is ExecutionResultCode.DirtyNode or ExecutionResultCode.DirtyBranch;
    }

    private void HandleCopiedNodesDeletionAttempt(ExecutableOperation<TId> operation)
    {
        _copiedNodes.RemoveLinksInBranch(operation.Model.Id);
    }

    private Task<T> ScheduleExecution<T>(Func<Task<T>> origin)
    {
        return _executionScheduler.Schedule(origin);
    }

    private Task<T> Schedule<T>(Func<T> origin, CancellationToken cancellationToken)
    {
        return _syncScheduler.Schedule(origin, cancellationToken);
    }

    private Task ScheduleAndCommit(Action origin, CancellationToken cancellationToken)
    {
        return _syncScheduler.ScheduleAndCommit(origin, cancellationToken);
    }

    private Task<T> ScheduleAndCommit<T>(Func<T> origin, CancellationToken cancellationToken)
    {
        return _syncScheduler.ScheduleAndCommit(origin, cancellationToken);
    }

    private void LogStartedOperationExecution(ExecutableOperation<TId> operation)
    {
        _startedLogging.LogOperation(ToOperation(operation));
    }

    private void LogFinishedOperationExecution(ExecutableOperation<TId> operation)
    {
        _finishedLogging.LogOperation(ToOperation(operation));
    }

    private Operation<FileSystemNodeModel<TId>> ToOperation(ExecutableOperation<TId> operation)
    {
        return new Operation<FileSystemNodeModel<TId>>(
            operation.Type,
            operation.Model);
    }
}
