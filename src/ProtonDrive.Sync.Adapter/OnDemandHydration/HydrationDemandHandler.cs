using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Adapter.OnDemandHydration.FileSizeCorrection;
using ProtonDrive.Sync.Adapter.Shared;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.OnDemandHydration;

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
    private readonly IFileSizeCorrector<TId, TAltId> _fileSizeCorrector;

    public HydrationDemandHandler(
        ILogger<HydrationDemandHandler<TId, TAltId>> logger,
        IScheduler executionScheduler,
        IScheduler syncScheduler,
        AdapterTree<TId, TAltId> adapterTree,
        IFileRevisionProvider<TId> fileRevisionProvider,
        IMappedNodeIdentityProvider<TId> mappedNodeIdProvider,
        IFileSizeCorrector<TId, TAltId> fileSizeCorrector)
    {
        _logger = logger;
        _executionScheduler = executionScheduler;
        _syncScheduler = syncScheduler;
        _adapterTree = adapterTree;
        _fileRevisionProvider = fileRevisionProvider;
        _mappedNodeIdProvider = mappedNodeIdProvider;
        _fileSizeCorrector = fileSizeCorrector;
    }

    public async Task HandleAsync(IFileHydrationDemand<TAltId> hydrationDemand, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fileNameToLog = _logger.GetSensitiveValueForLogging(hydrationDemand.FileInfo.Name);
        LogRequest();

        try
        {
            var nodeModel = await Schedule(() => Prepare(hydrationDemand), cancellationToken).ConfigureAwait(false);

            var mappedNodeId = await GetMappedNodeIdOrDefault(nodeModel.Id, cancellationToken).ConfigureAwait(false);
            if (mappedNodeId is null)
            {
                throw new HydrationException($"File with node Id={nodeModel.Id} {hydrationDemand.FileInfo.GetCompoundId()} is not mapped");
            }

            var initialLength = hydrationDemand.HydrationStream.Length;
            await HydrateFileAsync(hydrationDemand.HydrationStream, mappedNodeId.Value, nodeModel, cancellationToken).ConfigureAwait(false);

            var sizeDidNotMatch = hydrationDemand.HydrationStream.Length != initialLength;
            if (sizeDidNotMatch)
            {
                LogSizeMismatch(nodeModel.Id);

                await ScheduleExecution(() => CorrectFileSize(nodeModel, hydrationDemand, cancellationToken), cancellationToken).ConfigureAwait(false);

                return;
            }

            LogSuccess(nodeModel.Id);
        }
        catch (OperationCanceledException)
        {
            LogCancellation();
        }

        void LogRequest()
        {
            _logger.LogInformation(
                "Requested on-demand hydration of \"{FileName}\" with external Id={ExternalId}",
                fileNameToLog,
                hydrationDemand.FileInfo.GetCompoundId());
        }

        void LogSuccess(TId nodeId)
        {
            _logger.LogInformation(
                "On-demand hydration of \"{FileName}\" with Id={Id} {ExternalId} succeeded",
                fileNameToLog,
                nodeId,
                hydrationDemand.FileInfo.GetCompoundId());
        }

        void LogSizeMismatch(TId nodeId)
        {
            _logger.LogInformation(
                "On-demand hydration of \"{FileName}\" with Id={Id} {ExternalId} requires size correction",
                fileNameToLog,
                nodeId,
                hydrationDemand.FileInfo.GetCompoundId());
        }

        void LogCancellation()
        {
            _logger.LogInformation(
                "On-demand hydration of \"{FileName}\" with external Id={ExternalId} was cancelled",
                fileNameToLog,
                hydrationDemand.FileInfo.Id);
        }
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

    private bool ContentHasDiverged(AdapterTreeNode<TId, TAltId> node, NodeInfo<TAltId> nodeInfo)
    {
        return node.Model.Size != nodeInfo.Size || node.Model.LastWriteTime != nodeInfo.LastWriteTimeUtc;
    }

    private Task<TId?> GetMappedNodeIdOrDefault(TId nodeId, CancellationToken cancellationToken)
    {
        return _mappedNodeIdProvider.GetMappedNodeIdOrDefaultAsync(nodeId, cancellationToken);
    }

    private async Task HydrateFileAsync(Stream destinationContent, TId mappedNodeId, AdapterTreeNodeModel<TId, TAltId> nodeModel, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sourceRevision = await _fileRevisionProvider.OpenFileForReadingAsync(mappedNodeId, nodeModel.ContentVersion, cancellationToken).ConfigureAwait(false);
        await using (sourceRevision.ConfigureAwait(false))
        {
            var sourceContent = sourceRevision.GetContentStream();
            await using (sourceContent.ConfigureAwait(false))
            {
                await sourceContent.CopyToAsync(destinationContent, cancellationToken).ConfigureAwait(false);

                if (destinationContent.Position < destinationContent.Length)
                {
                    destinationContent.SetLength(destinationContent.Position);
                }
            }
        }
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
