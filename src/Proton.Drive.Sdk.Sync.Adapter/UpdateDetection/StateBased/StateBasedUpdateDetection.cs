using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.Adapter.Trees.Dirty;
using Proton.Drive.Sdk.Sync.Adapter.UpdateDetection.StateBased.Enumeration;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Collections.Generic;
using Proton.Drive.Sdk.Sync.Shared.ExecutionStatistics;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection.StateBased;

internal sealed class StateBasedUpdateDetection<TId, TAltId> : IExecutionStatisticsProvider
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<StateBasedUpdateDetection<TId, TAltId>> _logger;
    private readonly Replica _replica;
    private readonly IScheduler _executionScheduler;
    private readonly IScheduler _syncScheduler;
    private readonly IReadOnlyDictionary<TId, RootInfo<TAltId>> _syncRoots;
    private readonly RootEnumeration<TId, TAltId> _rootEnumeration;
    private readonly DirtyNodeUpdateDetection<TId, TAltId> _dirtyNodeUpdateDetection;
    private readonly DeletionDetection<TId, TAltId> _deletionDetection;
    private readonly ConcurrentExecutionStatistics _executionStatistics;

    private readonly DirtyNodesTraversal<TId, TAltId> _dirtyNodesTraversal;

    public StateBasedUpdateDetection(
        ILogger<StateBasedUpdateDetection<TId, TAltId>> logger,
        Replica replica,
        IScheduler executionScheduler,
        IScheduler syncScheduler,
        AdapterTree<TId, TAltId> adapterTree,
        DirtyTree<TId> dirtyTree,
        IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots,
        RootEnumeration<TId, TAltId> rootEnumeration,
        DirtyNodeUpdateDetection<TId, TAltId> dirtyNodeUpdateDetection,
        DeletionDetection<TId, TAltId> deletionDetection,
        ConcurrentExecutionStatistics executionStatistics)
    {
        _logger = logger;
        _replica = replica;
        _executionScheduler = executionScheduler;
        _syncScheduler = syncScheduler;
        _syncRoots = syncRoots;
        _rootEnumeration = rootEnumeration;
        _dirtyNodeUpdateDetection = dirtyNodeUpdateDetection;
        _deletionDetection = deletionDetection;
        _executionStatistics = executionStatistics;

        _dirtyNodesTraversal = new DirtyNodesTraversal<TId, TAltId>(adapterTree, dirtyTree);
    }

    public IExecutionStatistics ExecutionStatistics => _executionStatistics;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting {Replica} state-based update detection", _replica);

        await DetectRootUpdates(cancellationToken).ConfigureAwait(false);
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _executionStatistics.ClearFailures();
        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            foreach (var (syncRootNodeId, syncRoot) in _syncRoots)
            {
                if (!syncRoot.IsEnabled)
                {
                    continue;
                }

                await foreach (var node in DirtyNodes(syncRootNodeId, cancellationToken).ConfigureAwait(false))
                {
                    await DetectUpdates(node, cancellationToken).ConfigureAwait(false);
                }
            }

            await _deletionDetection.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _logger.LogInformation(
                "Finished {Replica} state-based update detection in {ElapsedTime}",
                _replica,
                Stopwatch.GetElapsedTime(startTimestamp));
        }
    }

    private Task DetectRootUpdates(CancellationToken cancellationToken)
    {
        return ScheduleExecution(() => _rootEnumeration.ExecuteAsync(cancellationToken));
    }

    private Task DetectUpdates(AdapterTreeNode<TId, TAltId> node, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ScheduleExecution(() => _dirtyNodeUpdateDetection.DetectUpdates(node, cancellationToken));
    }

    private IAsyncEnumerable<AdapterTreeNode<TId, TAltId>> DirtyNodes(TId syncRootNodeId, CancellationToken cancellationToken)
    {
        return new ScheduledEnumerable<AdapterTreeNode<TId, TAltId>>(_syncScheduler, _dirtyNodesTraversal.DirtyNodes(syncRootNodeId, cancellationToken));
    }

    private Task ScheduleExecution(Func<Task> origin)
    {
        return _executionScheduler.Schedule(origin);
    }
}
