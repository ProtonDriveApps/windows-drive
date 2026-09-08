using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Adapter.OnDemandHydration.FileSizeCorrection;
using Proton.Drive.Sdk.Sync.Adapter.Shared;
using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.Shared.Adapters;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.SyncActivity;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Shared.IO;
using Proton.Drive.Shared.Logging;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Adapter.OnDemandHydration;

internal sealed class HydrationDemandHandler<TId, TAltId> : IFileHydrationDemandHandler<TAltId>
    where TId : struct, IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<HydrationDemandHandler<TId, TAltId>> _logger;
    private readonly IScheduler _executionScheduler;
    private readonly IScheduler _syncScheduler;
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly IFileRevisionProvider<TId> _fileRevisionProvider;
    private readonly IMappedNodeIdentityProvider<TId> _mappedNodeIdProvider;
    private readonly ICopySourceRevisionProvider<TId, TAltId> _copySourceRevisionProvider;
    private readonly IFileSizeCorrector<TId, TAltId> _fileSizeCorrector;
    private readonly SyncActivity<TId> _syncActivity;

    public HydrationDemandHandler(
        ILogger<HydrationDemandHandler<TId, TAltId>> logger,
        IScheduler executionScheduler,
        IScheduler syncScheduler,
        AdapterTree<TId, TAltId> adapterTree,
        IFileRevisionProvider<TId> fileRevisionProvider,
        IMappedNodeIdentityProvider<TId> mappedNodeIdProvider,
        ICopySourceRevisionProvider<TId, TAltId> copySourceRevisionProvider,
        IFileSizeCorrector<TId, TAltId> fileSizeCorrector,
        SyncActivity<TId> syncActivity)
    {
        _logger = logger;
        _executionScheduler = executionScheduler;
        _syncScheduler = syncScheduler;
        _adapterTree = adapterTree;
        _fileRevisionProvider = fileRevisionProvider;
        _mappedNodeIdProvider = mappedNodeIdProvider;
        _copySourceRevisionProvider = copySourceRevisionProvider;
        _fileSizeCorrector = fileSizeCorrector;
        _syncActivity = syncActivity;
    }

    public async Task HandleAsync(IFileHydrationDemand<TAltId> hydrationDemand, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fileNameToLog = _logger.GetSensitiveValueForLogging(hydrationDemand.FileInfo.Name);
        LogRequest();
        var startTimestamp = Stopwatch.GetTimestamp();

        var nodeModel = await Schedule(() => Prepare(hydrationDemand), cancellationToken).ConfigureAwait(false);

        var syncActivityItem = nodeModel.GetSyncActivityItemForFileHydration(hydrationDemand.FileInfo);

        try
        {
            _syncActivity.OnProgress(syncActivityItem, Progress.Zero);

            hydrationDemand.ThrowIfInsufficientLocalFreeSpace();

            var sourceRevision = await OpenFileForReadingAsync(nodeModel, cancellationToken).ConfigureAwait(false);

            await using (sourceRevision.ConfigureAwait(false))
            {
                syncActivityItem = syncActivityItem with
                {
                    Stage = SyncActivityStage.Execution,
                };

                _syncActivity.OnProgress(syncActivityItem, Progress.Zero);

                var expectedChecksum = hydrationDemand.ChecksumVerificationEnabled
                    ? await sourceRevision.GetContentChecksumAsync(cancellationToken).ConfigureAwait(false)
                    : FileContentChecksum.Empty;

                var hydrationStream = hydrationDemand.GetHydrationStream(expectedChecksum);
                var destinationStream = new WriteOnlyProgressReportingStream(hydrationStream, NotifyProgressChanged);

                await using (destinationStream.ConfigureAwait(false))
                {
                    var initialPlaceholderSize = destinationStream.Length;

                    await HydrateFileAsync(destinationStream, sourceRevision, cancellationToken).ConfigureAwait(false);

                    var actualHydrationLength = destinationStream.Position;

                    // When the file size extended attribute is missing, the initial placeholder size is likely to be too large and requires correction
                    if (actualHydrationLength < initialPlaceholderSize)
                    {
                        destinationStream.SetLength(actualHydrationLength);
                    }

                    var sizeMismatch = destinationStream.Length - initialPlaceholderSize;
                    if (sizeMismatch != 0)
                    {
                        LogSizeMismatch(nodeModel.Id, sizeMismatch);

                        await ScheduleExecution(() => CorrectFileSize(nodeModel, hydrationDemand, cancellationToken), cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            _syncActivity.OnChanged(syncActivityItem, SyncActivityItemStatus.Succeeded);

            LogSuccess(nodeModel.Id, Stopwatch.GetElapsedTime(startTimestamp));
        }
        catch (Exception ex)
        {
            var (errorCode, errorMessage) = ex.GetErrorInfo();

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

            if (ex is not OperationCanceledException)
            {
                throw;
            }

            LogCancellation(errorCode, errorMessage);
        }

        return;

        void NotifyProgressChanged(Progress value)
        {
            _syncActivity.OnProgress(syncActivityItem, value);
        }

        void LogRequest()
        {
            _logger.LogInformation(
                "Requested on-demand hydration of \"{FileName}\" with external Id={ExternalId}",
                fileNameToLog,
                hydrationDemand.FileInfo.GetCompoundId());
        }

        void LogSuccess(TId nodeId, TimeSpan elapsedTime)
        {
            _logger.LogInformation(
                "On-demand hydration of \"{FileName}\" with Id=\"{Root}\"/{Id} {ExternalId} succeeded in {ElapsedTime}",
                fileNameToLog,
                hydrationDemand.FileInfo.Root?.Id,
                nodeId,
                hydrationDemand.FileInfo.GetCompoundId(),
                elapsedTime);
        }

        void LogSizeMismatch(TId nodeId, long mismatch)
        {
            _logger.LogInformation(
                "On-demand hydration of \"{FileName}\" with Id=\"{Root}\"/{Id} {ExternalId} requires size correction by {Mismatch}",
                fileNameToLog,
                hydrationDemand.FileInfo.Root?.Id,
                nodeId,
                hydrationDemand.FileInfo.GetCompoundId(),
                mismatch);
        }

        void LogCancellation(FileSystemErrorCode errorCode, string? errorMessage)
        {
            _logger.LogInformation(
                "On-demand hydration of \"{FileName}\" with external Id={ExternalId} was cancelled: {ErrorCode} ({ErrorMessage})",
                fileNameToLog,
                hydrationDemand.FileInfo.GetCompoundId(),
                errorCode,
                errorMessage);
        }
    }

    private static bool ContentHasDiverged(AdapterTreeNode<TId, TAltId> node, NodeInfo<TAltId> nodeInfo)
    {
        return node.Model.Size != nodeInfo.Size || node.Model.LastWriteTime != nodeInfo.LastWriteTimeUtc;
    }

    private AdapterTreeNodeModel<TId, TAltId> Prepare(IFileHydrationDemand<TAltId> hydrationDemand)
    {
        var altId = hydrationDemand.FileInfo.GetCompoundId();

        if (altId.IsDefault())
        {
            throw new InvalidOperationException($"No identifier given for file \"{_logger.GetSensitiveValueForLogging(hydrationDemand.FileInfo.Name)}\"");
        }

        var node = _adapterTree.NodeByAltIdOrDefault(altId)
                   ?? throw new HydrationException($"Adapter Tree node with AltId={altId} does not exist");

        if (node.Type != NodeType.File)
        {
            throw new HydrationException($"Adapter Tree node with Id={node.Id} {altId} is not a file");
        }

        if (node.IsNodeOrBranchDeleted())
        {
            throw new HydrationException($"Adapter Tree node with Id={node.Id} {altId} or branch is deleted");
        }

        if (ContentHasDiverged(node, hydrationDemand.FileInfo))
        {
            throw new HydrationException($"File with node Id={node.Id} {altId} content has diverged");
        }

        return node.Model;
    }

    private Task CorrectFileSize(AdapterTreeNodeModel<TId, TAltId> nodeModel, IFileHydrationDemand<TAltId> hydrationDemand, CancellationToken cancellationToken)
    {
        return _fileSizeCorrector.UpdateSizeAsync(nodeModel, hydrationDemand, cancellationToken);
    }

    private async Task<ISourceRevision> OpenFileForReadingAsync(AdapterTreeNodeModel<TId, TAltId> nodeModel, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var mappedNodeId = await _mappedNodeIdProvider.GetMappedNodeIdOrDefaultAsync(nodeModel.Id, cancellationToken).ConfigureAwait(false);
        if (mappedNodeId is not null)
        {
            return await _fileRevisionProvider.OpenFileForReadingAsync(mappedNodeId.Value, nodeModel.ContentVersion, cancellationToken).ConfigureAwait(false);
        }

        // The node can be a not-yet-synced destination of a copy that replaced a move between volumes
        // (for example between two "shared with me" items).
        // The content is still available on the copy source node.
        var result = await _copySourceRevisionProvider.OpenForReadingAsync(nodeModel.Id, cancellationToken).ConfigureAwait(false);

        if (result is null)
        {
            throw new HydrationException(
                $"File with Adapter Tree node Id={nodeModel.Id} is not mapped",
                new FileSystemClientException(string.Empty, FileSystemErrorCode.ObjectNotFound));
        }

        return result;
    }

    private Task HydrateFileAsync(Stream destination, ISourceRevision source, CancellationToken cancellationToken)
    {
        return source.CopyContentToAsync(destination, cancellationToken);
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    private Task ScheduleExecution(Func<Task> origin, CancellationToken cancellationToken)
    {
        return _executionScheduler.Schedule(origin, cancellationToken);
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    private Task<T> Schedule<T>(Func<T> origin, CancellationToken cancellationToken)
    {
        return _syncScheduler.Schedule(origin, cancellationToken);
    }
}
