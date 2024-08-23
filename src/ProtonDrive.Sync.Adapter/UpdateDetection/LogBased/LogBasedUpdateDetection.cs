using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Adapter.NodeCopying;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.ExecutionStatistics;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Adapter.UpdateDetection.LogBased;

internal class LogBasedUpdateDetection<TId, TAltId> : IExecutionStatisticsProvider
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger _logger;
    private readonly IScheduler _executionScheduler;
    private readonly IScheduler _syncScheduler;
    private readonly IEventLogClient<TAltId> _eventLogClient;
    private readonly UpdateDetectionSequencer _updateDetectionSequencer;

    private readonly LogBasedUpdateDetectionExecutionStatistics _executionStatistics = new();
    private readonly ConcurrentQueue<ReceivedEventLogEntries<TAltId>> _eventLogQueue = new();
    private readonly CoalescingAction _updateDetection;
    private readonly IdentityBasedEventLogProcessingStep<TId, TAltId> _eventLogProcessingStep;

    private bool _started;
    private bool _isFaulty;
    private ReceivedEventLogEntries<TAltId>? _logEntries;

    public LogBasedUpdateDetection(
        ILoggerFactory loggerFactory,
        IScheduler executionScheduler,
        IScheduler syncScheduler,
        AdapterTree<TId, TAltId> adapterTree,
        IDirtyNodes<TId, TAltId> dirtyNodes,
        IEventLogClient<TAltId> eventLogClient,
        IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots,
        IIdentitySource<TId> idSource,
        NodeUpdateDetection<TId, TAltId> nodeUpdateDetection,
        FileVersionMapping<TId, TAltId> fileVersionMapping,
        ICopiedNodes<TId, TAltId> copiedNodes,
        IItemExclusionFilter itemExclusionFilter,
        UpdateDetectionSequencer updateDetectionSequencer)
    {
        _logger = loggerFactory.CreateLogger<LogBasedUpdateDetection<TId, TAltId>>();
        _executionScheduler = executionScheduler;
        _syncScheduler = syncScheduler;
        _eventLogClient = eventLogClient;
        _updateDetectionSequencer = updateDetectionSequencer;

        _updateDetection = new CoalescingAction(ProcessAllLogEntries);
        eventLogClient.LogEntriesReceived += OnEventLogClientNextEntries;
        updateDetectionSequencer.Resumed += OnUpdateDetectionSequencerResumed;

        _eventLogProcessingStep = new IdentityBasedEventLogProcessingStep<TId, TAltId>(
            loggerFactory.CreateLogger<IdentityBasedEventLogProcessingStep<TId, TAltId>>(),
            adapterTree,
            dirtyNodes,
            idSource,
            nodeUpdateDetection,
            fileVersionMapping,
            syncRoots,
            copiedNodes,
            itemExclusionFilter);
    }

    public IExecutionStatistics ExecutionStatistics => _executionStatistics;

    public void Start()
    {
        _logger.LogInformation("Starting event log-based update detection");
        _started = true;

        _eventLogClient.Enable();
    }

    public Task StopAsync()
    {
        _logger.LogInformation("Stopping event log-based update detection");
        _started = false;

        _eventLogClient.Disable();

        _updateDetection.Cancel();
        return _updateDetection.CurrentTask;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // The events retrieval is not cancelled, but the caller cancels awaiting it.
        await _eventLogClient.GetEventsAsync().WithCancellation(cancellationToken).ConfigureAwait(false);

        // The Log-based updated detection is not cancelled, but the caller cancels awaiting it.
        await _updateDetection.CurrentTask.WithCancellation(cancellationToken).ConfigureAwait(false);
    }

    private void OnEventLogClientNextEntries(object? sender, EventLogEntriesReceivedEventArgs<TAltId> e)
    {
        if (_isFaulty)
        {
            return;
        }

        _eventLogQueue.Enqueue(
            new ReceivedEventLogEntries<TAltId>(e.Entries, e.VolumeId, e.Scope, _updateDetectionSequencer.GetTimestamp(), e.ConsiderEventsProcessed));

        _ = _updateDetection.Run();
    }

    private void OnUpdateDetectionSequencerResumed(object? sender, EventArgs e)
    {
        if (_isFaulty)
        {
            return;
        }

        _ = _updateDetection.Run();
    }

    private Task ProcessAllLogEntries(CancellationToken cancellationToken)
    {
        if (!_started)
        {
            _logger.LogWarning("Event log-based update detection is not started");

            return Task.CompletedTask;
        }

        if (_isFaulty)
        {
            return Task.CompletedTask;
        }

        return
            WithLoggedException(() =>
                WithSafeCancellation(() =>
                    WithFaultyStateDetection(() =>
                        ScheduleExecution(() =>
                            Schedule(InternalProcessAllLogEntries)))));

        async Task InternalProcessAllLogEntries()
        {
            if (!await ProcessLogEntries(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            while (GetLogEntries() && await ProcessLogEntries(cancellationToken).ConfigureAwait(false))
            {
            }
        }
    }

    private bool GetLogEntries()
    {
        return _eventLogQueue.TryDequeue(out _logEntries);
    }

    private async Task<bool> ProcessLogEntries(CancellationToken cancellationToken)
    {
        if (_logEntries == null)
        {
            return true;
        }

        var hasError = false;

        if (await _updateDetectionSequencer.IsUpdateDetectionPostponedAsync(_logEntries.Timestamp, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        foreach (var entry in _logEntries.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _eventLogProcessingStep.Execute(_logEntries.VolumeId, _logEntries.Scope, entry);

            if (entry.ChangeType == EventLogChangeType.Error)
            {
                // The presence of the event log entry of type Error indicates failure
                // to retrieve event log entries, the absence indicates success.
                hasError = true;
            }
        }

        _logEntries.OnProcessed();

        // The error is reset with each batch of event log entries
        if (hasError)
        {
            _executionStatistics.Failed();
        }
        else
        {
            _executionStatistics.Succeeded();
        }

        _logEntries = null;
        return true;
    }

    private void Fault()
    {
        var faulted = !_isFaulty;
        _isFaulty = true;

        if (faulted)
        {
            _logger.LogWarning("{ComponentName} has faulted", nameof(LogBasedUpdateDetection<TId, TAltId>));
        }
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
            _logger.LogWarning("{ComponentName} operation failed: {ErrorMessage}", nameof(LogBasedUpdateDetection<TId, TAltId>), ex.Message);

            Fault();
        }
    }

    private Task WithSafeCancellation(Func<Task> origin)
    {
        return _logger.WithSafeCancellation(origin, nameof(LogBasedUpdateDetection<TId, TAltId>));
    }

    private Task WithLoggedException(Func<Task> origin)
    {
        return _logger.WithLoggedException(origin, $"{nameof(LogBasedUpdateDetection<TId, TAltId>)} operation failed", includeStackTrace: true);
    }

    private Task ScheduleExecution(Func<Task> origin)
    {
        return _executionScheduler.Schedule(origin);
    }

    private Task Schedule(Func<Task> origin)
    {
        return _syncScheduler.Schedule(origin);
    }
}
