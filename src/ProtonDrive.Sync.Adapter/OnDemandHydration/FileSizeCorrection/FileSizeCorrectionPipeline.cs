using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Adapter.UpdateDetection;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.OnDemandHydration.FileSizeCorrection;

internal sealed class FileSizeCorrectionPipeline<TId, TAltId> : IFileSizeCorrector<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<FileSizeCorrectionPipeline<TId, TAltId>> _logger;
    private readonly ITransactedScheduler _syncScheduler;
    private readonly UpdateDetectionSequencer _updateDetectionSequencer;
    private readonly PreconditionsValidationStep<TId, TAltId> _preconditionsValidation;
    private readonly OperationExecution.PreparationStep<TId, TAltId> _preparation;
    private readonly OperationExecution.SuccessStep<TId, TAltId> _success;

    public FileSizeCorrectionPipeline(
        ILogger<FileSizeCorrectionPipeline<TId, TAltId>> logger,
        ITransactedScheduler syncScheduler,
        UpdateDetectionSequencer updateDetectionSequencer,
        PreconditionsValidationStep<TId, TAltId> preconditionsValidation,
        OperationExecution.PreparationStep<TId, TAltId> preparation,
        OperationExecution.SuccessStep<TId, TAltId> success)
    {
        _logger = logger;
        _syncScheduler = syncScheduler;
        _updateDetectionSequencer = updateDetectionSequencer;
        _preconditionsValidation = preconditionsValidation;
        _preparation = preparation;
        _success = success;
    }

    public async Task UpdateSizeAsync(
        AdapterTreeNodeModel<TId, TAltId> initialNodeModel,
        IFileHydrationDemand<TAltId> hydrationDemand,
        CancellationToken cancellationToken)
    {
        var (operation, nodeInfo) = await Schedule(() => Prepare(initialNodeModel), cancellationToken).ConfigureAwait(false);

        if (operation is null || nodeInfo is null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var updateDetection = new UpdateDetectionSwitch(_updateDetectionSequencer);

        var (finalNodeInfo, exception) = await Execute(hydrationDemand, updateDetection, cancellationToken).ConfigureAwait(false);

        if (finalNodeInfo is null)
        {
            LogFailure(operation, nodeInfo, exception!);

            return;
        }

        // To make sure the Adapter Tree node update is persisted, the cancellation token is not passed
        await ScheduleAndCommit(() => HandleSuccess(operation, finalNodeInfo), CancellationToken.None).ConfigureAwait(false);
    }

    private static ExecutableOperation<TId> ToExecutableOperation(IFileSystemNodeModel<TId> nodeModel)
    {
        return new ExecutableOperation<TId>(
            OperationType.Edit,
            new AltIdentifiableFileSystemNodeModel<TId, TId>().CopiedFrom(nodeModel),
            backup: false);
    }

    private (ExecutableOperation<TId>? Operation, NodeInfo<TAltId>? NodeInfo) Prepare(AdapterTreeNodeModel<TId, TAltId> initialNodeModel)
    {
        if (!ValidatePreconditions(initialNodeModel))
        {
            return default;
        }

        var operation = ToExecutableOperation(initialNodeModel);
        var (nodeInfo, _) = _preparation.Execute(operation);

        return (operation, nodeInfo);
    }

    private bool ValidatePreconditions(AdapterTreeNodeModel<TId, TAltId> initialNodeModel)
    {
        if (!_preconditionsValidation.Execute(initialNodeModel))
        {
            var fileNameToLog = _logger.GetSensitiveValueForLogging(initialNodeModel.Name);
            _logger.LogInformation(
                "Skipping size correction of \"{FileName}\" with Id={Id} ({ExternalId}), Adapter Tree node has diverged",
                fileNameToLog,
                initialNodeModel.Id,
                initialNodeModel.AltId);

            return false;
        }

        return true;
    }

    private async Task<(NodeInfo<TAltId>? Value, Exception? Exception)> Execute(
        IFileHydrationDemand<TAltId> hydrationRequest,
        UpdateDetectionSwitch updateDetection,
        CancellationToken cancellationToken)
    {
        try
        {
            // Updating placeholder file size generates file change events on local file system.
            // To make sure operation execution result is applied to the Adapter Tree before
            // processing those events, event log based update detection is postponed until
            // file size correction is finished and the result is applied to the Adapter Tree.
            await updateDetection.PostponeAsync(cancellationToken).ConfigureAwait(false);

            var finalNodeInfo = hydrationRequest.UpdateFileSize();

            return (finalNodeInfo, Exception: null);
        }
        catch (Exception ex) when (ex is FileSystemClientException)
        {
            return (null, ex);
        }
        catch (OperationCanceledException ex)
        {
            return (null, ex);
        }
    }

    private void HandleSuccess(ExecutableOperation<TId> operation, NodeInfo<TAltId> finalNodeInfo)
    {
        LogSuccess(operation, finalNodeInfo);

        if (!_preconditionsValidation.ExecuteBeforeApplyingResult(operation))
        {
            _logger.LogWarning("Failed to update Adapter Tree node with Id={Id}, tree state has diverged", operation.Model.Id);

            return;
        }

        _success.Execute(operation, finalNodeInfo);
    }

    private void LogSuccess(ExecutableOperation<TId> operation, NodeInfo<TAltId> nodeInfo)
    {
        var nameToLog = _logger.GetSensitiveValueForLogging(nodeInfo.Name);
        _logger.LogInformation(
            "Corrected size of file \"{FileName}\" with Id={Id} ({ExternalId}), ContentVersion={ContentVersion}",
            nameToLog,
            operation.Model.Id,
            nodeInfo.Id,
            operation.Model.ContentVersion);
    }

    private void LogFailure(ExecutableOperation<TId> operation, NodeInfo<TAltId> nodeInfo, Exception exception)
    {
        var nameToLog = _logger.GetSensitiveValueForLogging(nodeInfo.Name);
        _logger.LogInformation(
            "Failed to correct size of file \"{FileName}\" with Id={Id} ({ExternalId}), ContentVersion={ContentVersion}: {ErrorMessage}",
            nameToLog,
            operation.Model.Id,
            nodeInfo.Id,
            operation.Model.ContentVersion,
            exception.CombinedMessage());
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    private Task<TResult> Schedule<TResult>(Func<TResult> origin, CancellationToken cancellationToken)
    {
        return _syncScheduler.Schedule(origin, cancellationToken);
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    private Task ScheduleAndCommit(Action origin, CancellationToken cancellationToken)
    {
        return _syncScheduler.ScheduleAndCommit(origin, cancellationToken);
    }
}
