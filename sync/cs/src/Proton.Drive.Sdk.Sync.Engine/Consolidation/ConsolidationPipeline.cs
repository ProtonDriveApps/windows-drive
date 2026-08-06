using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Synced;
using Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Update;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Trees.Changes;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.Operations;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Engine.Consolidation;

internal class ConsolidationPipeline<TId>
    where TId : IEquatable<TId>, IComparable<TId>
{
    private readonly Replica _replica;
    private readonly IReceivedTreeChanges<TId> _detectedUpdates;
    private readonly IScheduler _syncScheduler;
    private readonly ILogger<ConsolidationPipeline<TId>> _logger;

    private readonly UpdateConsolidationPipeline<TId> _updateConsolidationStep;
    private readonly UpdateStatisticsCalculator<TId> _updateStatisticsCalculator;

    public ConsolidationPipeline(
        Replica replica,
        IReceivedTreeChanges<TId> detectedUpdates,
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> updateTree,
        IScheduler syncScheduler,
        ILogger<ConsolidationPipeline<TId>> logger)
    {
        _replica = replica;
        _detectedUpdates = detectedUpdates;
        _syncScheduler = syncScheduler;
        _logger = logger;

        _updateConsolidationStep = new UpdateConsolidationPipeline<TId>(replica, syncedTree, updateTree, logger);
        _updateStatisticsCalculator = new UpdateStatisticsCalculator<TId>(replica, updateTree, logger);
    }

    public async Task Execute(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Started {Replica} consolidation", _replica);
        var startTimestamp = Stopwatch.GetTimestamp();

        await Schedule(ExecuteInternal, cancellationToken).ConfigureAwait(false);
        await Schedule(CalculateStatistics, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Finished {Replica} consolidation in {ElapsedTime}", _replica, Stopwatch.GetElapsedTime(startTimestamp));
    }

    public void ExecuteInternal(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var detectedUpdate in _detectedUpdates)
        {
            Execute(detectedUpdate.Operation);

            _detectedUpdates.AcknowledgeConsumed(detectedUpdate);

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private void Execute(Operation<FileSystemNodeModel<TId>> detectedUpdate)
    {
        _updateConsolidationStep.Execute(detectedUpdate);
    }

    private void CalculateStatistics(CancellationToken cancellationToken)
    {
        _updateStatisticsCalculator.Execute(cancellationToken);
    }

    private Task Schedule(Action<CancellationToken> origin, CancellationToken cancellationToken)
    {
        return _syncScheduler.Schedule(() => origin(cancellationToken), cancellationToken);
    }
}
