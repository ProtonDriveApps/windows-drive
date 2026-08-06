using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Adapter;
using Proton.Drive.Sdk.Sync.Adapter.Shared;
using Proton.Drive.Sdk.Sync.Agent.Health;
using Proton.Drive.Sdk.Sync.Agent.Health.FileConsistency;
using Proton.Drive.Sdk.Sync.DataAccess.Databases;
using Proton.Drive.Sdk.Sync.Engine;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.ExecutionStatistics;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.SyncActivity;
using Proton.Drive.Sdk.Sync.Shared.Trees;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Telemetry;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Agent;

internal class SyncAgent : IDisposable
{
    private static readonly TimeSpan SynchronizationDelayInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SynchronizationRetryInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SynchronizationTimerInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StatisticsPollInterval = TimeSpan.FromMilliseconds(300);

    private readonly GenericAdapter<long, string> _remoteAdapter;
    private readonly GenericAdapter<long, long> _localAdapter;
    private readonly SyncEngine<long> _syncEngine;
    private readonly RemoteAdapterDatabase _remoteAdapterDatabase;
    private readonly LocalAdapterDatabase _localAdapterDatabase;
    private readonly SyncEngineDatabase _syncEngineDatabase;
    private readonly StateConsistencyGuard<long> _stateConsistencyGuard;
    private readonly IFileConsistencyGuard _fileConsistencyGuard;
    private readonly IClock _clock;
    private readonly IErrorCounter _errorCounter;
    private readonly ILogger<SyncAgent> _logger;

    private readonly SingleAction _synchronization;
    private readonly ISchedulerTimer _synchronizationTimer;
    private readonly ISchedulerTimer _updateStatisticsTimer;
    private readonly ConcurrentExecutionStatistics _globalFailureStatistics = new();

    private bool _initialized;
    private bool _paused;
    private bool _isFaulty;
    private SyncStatus _syncStatus;
    private IExecutionStatistics _initialExecutionStatistics = IExecutionStatistics.Zero;
    private IExecutionStatistics _executionStatistics = IExecutionStatistics.Zero;
    private TickCount _updatesToSynchronizeDetectedAt;
    private TickCount _updateDetectionCompletedAt;
    private TickCount _synchronizationCompletedAt;
    private Task _syncEngineTask = Task.CompletedTask;

    public SyncAgent(
        GenericAdapter<long, string> remoteAdapter,
        GenericAdapter<long, long> localAdapter,
        SyncEngine<long> syncEngine,
        RemoteAdapterDatabase remoteAdapterDatabase,
        LocalAdapterDatabase localAdapterDatabase,
        SyncEngineDatabase syncEngineDatabase,
        StateConsistencyGuard<long> stateConsistencyGuard,
        IFileConsistencyGuard fileConsistencyGuard,
        IScheduler scheduler,
        IClock clock,
        IErrorCounter errorCounter,
        ILogger<SyncAgent> logger)
    {
        _remoteAdapter = remoteAdapter;
        _localAdapter = localAdapter;
        _syncEngine = syncEngine;
        _remoteAdapterDatabase = remoteAdapterDatabase;
        _localAdapterDatabase = localAdapterDatabase;
        _syncEngineDatabase = syncEngineDatabase;
        _stateConsistencyGuard = stateConsistencyGuard;
        _fileConsistencyGuard = fileConsistencyGuard;
        _clock = clock;
        _errorCounter = errorCounter;
        _logger = logger;

        _synchronization = new SingleAction(InternalSynchronizeAsync);

        _remoteAdapter.SyncActivityChanged += OnRemoteAdapterSyncActivityChanged;
        _localAdapter.SyncActivityChanged += OnLocalAdapterSyncActivityChanged;

        _synchronizationTimer = scheduler.CreateTimer();
        _synchronizationTimer.Tick += OnSynchronizationTimerTick;
        _synchronizationTimer.Interval = SynchronizationTimerInterval;

        _updateStatisticsTimer = scheduler.CreateTimer();
        _updateStatisticsTimer.Interval = StatisticsPollInterval;
        _updateStatisticsTimer.Tick += OnUpdateStatisticsTimerTick;
    }

    public event EventHandler<bool>? AvailabilityChanged;

    public event EventHandler<SyncStatus>? StatusChanged;

    public event EventHandler<IExecutionStatistics>? StatisticsChanged;

    public event EventHandler<SyncActivityChangedEventArgs<long>>? SyncActivityChanged;

    public SyncStatus Status
    {
        get => _syncStatus;
        private set
        {
            if (value != _syncStatus)
            {
                _syncStatus = value;
                OnStatusChanged(value);
            }
        }
    }

    public bool Paused
    {
        get => _paused;
        set
        {
            _paused = value;

            if (value)
            {
                _synchronization.Cancel();
            }

            if (_initialized)
            {
                // Synchronization task updates the SyncStatus to reflect Paused value
                _synchronization.RunAsync();
            }
        }
    }

    public void Dispose()
    {
        StatusChanged = null;
        StatisticsChanged = null;
        SyncActivityChanged = null;

        _synchronizationTimer.Dispose();
        _updateStatisticsTimer.Dispose();

        _localAdapter.Dispose();
        _remoteAdapter.Dispose();
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            _logger.LogInformation("Synchronization already initialized");

            return true;
        }

        _logger.LogInformation("Started initializing synchronization");
        Status = SyncStatus.Initializing;

        _syncEngineDatabase.Open();
        _remoteAdapterDatabase.Open();
        _localAdapterDatabase.Open();

        _syncEngineDatabase.Faulted += OnDatabaseFaulted;
        _remoteAdapterDatabase.Faulted += OnDatabaseFaulted;
        _localAdapterDatabase.Faulted += OnDatabaseFaulted;

        await _stateConsistencyGuard.ExecuteAsync(cancellationToken).ConfigureAwait(false);

        _syncEngine.Initialize();

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _remoteAdapter.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await _localAdapter.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (AggregateException ex)
        {
            ex.Handle(HandleConnectException);
            return false;
        }
        catch (FileSystemClientException ex)
        {
            HandleConnectException(ex);
            return false;
        }

        _logger.LogInformation("Finished initializing synchronization");

        _initialized = true;

        AvailabilityChanged?.Invoke(this, true);

        cancellationToken.ThrowIfCancellationRequested();
        ResetExecutionStatistics();
        _synchronizationTimer.Start();
        Synchronize();

        _fileConsistencyGuard.StartExecuting();

        return true;
    }

    public void Synchronize()
    {
        _synchronization.RunAsync();
    }

    public async Task<bool> StopAsync()
    {
        AvailabilityChanged?.Invoke(this, false);

        if (Status is SyncStatus.Terminated)
        {
            _logger.LogWarning("Synchronization not initialized");

            return true;
        }

        _logger.LogInformation("Started terminating synchronization");

        _synchronizationTimer.Stop();
        _synchronization.Cancel();

        Status = SyncStatus.Terminating;

        var stopFileConsistencyGuardTask = _fileConsistencyGuard.StopExecutingAsync();

        await WaitSyncEngineToFinishSynchronizingAsync().ConfigureAwait(false);

        await stopFileConsistencyGuardTask.ConfigureAwait(false);

        try
        {
            await _remoteAdapter.DisconnectAsync().ConfigureAwait(false);
            await _localAdapter.DisconnectAsync().ConfigureAwait(false);
        }
        catch (AggregateException ex)
        {
            ex.Handle(HandleDisconnectException);
            return false;
        }
        catch (FileSystemClientException ex)
        {
            HandleDisconnectException(ex);
            return false;
        }

        await _synchronization.WaitForCompletionAsync().ConfigureAwait(false);

        // When disconnecting an adapter, it calls Sync Engine for acknowledging consumed Synced Tree changes.
        // As a result, the Sync Engine schedules database transaction for persisting data. We must wait for
        // the scheduled tasks to complete before closing databases.
        await _syncEngine.WaitForCompletionAsync().ConfigureAwait(false);

        _syncEngineDatabase.Faulted -= OnDatabaseFaulted;
        _remoteAdapterDatabase.Faulted -= OnDatabaseFaulted;
        _localAdapterDatabase.Faulted -= OnDatabaseFaulted;

        _remoteAdapterDatabase.Close();
        _localAdapterDatabase.Close();
        _syncEngineDatabase.Close();

        _initialized = false;
        Status = SyncStatus.Terminated;

        _logger.LogInformation("Finished terminating synchronization");

        return true;
    }

    public async Task<LooseCompoundAltIdentity<string>?> GetRemoteIdFromLocalIdOrDefaultAsync(
        LooseCompoundAltIdentity<long> localAltId,
        CancellationToken cancellationToken)
    {
        try
        {
            var localNodeId = await _localAdapter.GetNodeIdByAltIdOrDefaultAsync(localAltId, cancellationToken).ConfigureAwait(false);
            if (localNodeId is null)
            {
                return null;
            }

            var syncNodeAltId = await _syncEngine.GetMappedNodeIdOrDefaultAsync(Replica.Local, localNodeId.Value, cancellationToken).ConfigureAwait(false);
            if (syncNodeAltId is null)
            {
                return null;
            }

            var remoteNodeAltId = await _remoteAdapter.GetNodeAltIdByIdOrDefaultAsync(syncNodeAltId.Value, cancellationToken).ConfigureAwait(false);

            return remoteNodeAltId;
        }
        catch
        {
            return null;
        }
    }

    public async Task<LooseCompoundAltIdentity<string>?> GetRemoteIdFromNodeIdOrDefaultAsync(long remoteNodeId, CancellationToken cancellationToken)
    {
        try
        {
            var remoteNodeAltId = await _remoteAdapter.GetNodeAltIdByIdOrDefaultAsync(remoteNodeId, cancellationToken).ConfigureAwait(false);

            return remoteNodeAltId;
        }
        catch
        {
            return null;
        }
    }

    private async Task InternalSynchronizeAsync(CancellationToken cancellationToken)
    {
        if (!_initialized)
        {
            _logger.LogWarning("Synchronization not initialized");

            return;
        }

        if (_isFaulty)
        {
            _logger.LogWarning("Synchronization has failed due to faulty internal state");
            Status = SyncStatus.Failed;
            _synchronizationTimer.Stop();

            return;
        }

        if (Status is SyncStatus.Failed)
        {
            _logger.LogWarning("Synchronization has failed");
            _synchronizationTimer.Stop();

            return;
        }

        if (_paused)
        {
            _logger.LogInformation("Synchronization is paused");
            Status = SyncStatus.Paused;

            return;
        }

        ResetExecutionStatistics();
        _updateStatisticsTimer.Start();

        try
        {
            do
            {
                await DetectUpdatesAsync(cancellationToken).ConfigureAwait(false);
                await SynchronizeAsync(cancellationToken).ConfigureAwait(false);
            }
            while (await ShouldRetrySynchronization(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // Expected
            _logger.LogInformation("Synchronization was cancelled");
        }
        catch (Exception ex)
        {
            _errorCounter.Add(ErrorScope.Sync, ex);

            // Not expected. Internal state of the Adapters or Sync Engine might be broken.
            _logger.LogError(ex, "Synchronization failed");
            _globalFailureStatistics.Failed.Increment();
            _synchronizationTimer.Stop();
            Status = SyncStatus.Failed;
        }
        finally
        {
            _updateStatisticsTimer.Stop();
            UpdateExecutionStatistics();

            if (Status is not SyncStatus.Failed)
            {
                Status = _paused ? SyncStatus.Paused : SyncStatus.Idle;
            }
        }
    }

    private async Task DetectUpdatesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Status = SyncStatus.DetectingUpdates;

        _logger.LogInformation("Started detecting updates");

        await _remoteAdapter.DetectUpdatesAsync(cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        await _localAdapter.DetectUpdatesAsync(cancellationToken).ConfigureAwait(false);

        _updateDetectionCompletedAt = _clock.TickCount;

        _logger.LogInformation("Finished detecting updates");
    }

    private async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_localAdapter.HasUpdatesToSynchronize
            && !_remoteAdapter.HasUpdatesToSynchronize
            && !_syncEngine.HasNewUpdatesToSynchronize
            && !_syncEngine.HasOldUpdatesToSynchronize)
        {
            return;
        }

        Status = SyncStatus.Synchronizing;

        _syncEngineTask = _syncEngine.SynchronizeAsync(cancellationToken);
        await _syncEngineTask.ConfigureAwait(false);

        _synchronizationCompletedAt = _clock.TickCount;
    }

    private async Task<bool> ShouldRetrySynchronization(CancellationToken cancellationToken)
    {
        // Synchronization cycle is not retried if adapters have no updates to synchronize
        if (!_localAdapter.HasUpdatesToSynchronize &&
            !_remoteAdapter.HasUpdatesToSynchronize)
        {
            return false;
        }

        await Task.Delay(SynchronizationDelayInterval, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private async Task WaitSyncEngineToFinishSynchronizingAsync()
    {
        try
        {
            await _syncEngineTask.ConfigureAwait(false);
        }
        catch
        {
            // Same Task is awaited in the SynchronizeAsync method, which handles exceptions
        }
    }

    private void OnSynchronizationTimerTick(object? sender, EventArgs e)
    {
        if (_isFaulty)
        {
            _synchronization.Cancel();
            Synchronize();

            return;
        }

        if (!_initialized || Status is not SyncStatus.Idle and not SyncStatus.Paused and not SyncStatus.Failed)
        {
            return;
        }

        var now = _clock.TickCount;
        var hasUpdatesToDetect = _localAdapter.HasUpdatesToDetect ||
                                 _remoteAdapter.HasUpdatesToDetect;
        var hasNewUpdatesToSynchronize = _localAdapter.HasUpdatesToSynchronize ||
                                         _remoteAdapter.HasUpdatesToSynchronize ||
                                         _syncEngine.HasNewUpdatesToSynchronize;
        var hasOldUpdatesToSynchronize = _syncEngine.HasOldUpdatesToSynchronize;

        if (hasNewUpdatesToSynchronize && _updatesToSynchronizeDetectedAt == default)
        {
            _updatesToSynchronizeDetectedAt = now;
        }

        if (!hasNewUpdatesToSynchronize && _updatesToSynchronizeDetectedAt != default)
        {
            _updatesToSynchronizeDetectedAt = default;
        }

        if (Paused)
        {
            return;
        }

        var shouldDetectUpdates = hasUpdatesToDetect &&
                                  _updateDetectionCompletedAt + SynchronizationRetryInterval < now;

        var shouldSynchronizeNewUpdates = hasNewUpdatesToSynchronize &&
                                          _updatesToSynchronizeDetectedAt + SynchronizationDelayInterval < now;

        var shouldSynchronizeOldUpdates = hasOldUpdatesToSynchronize &&
                                          _synchronizationCompletedAt + SynchronizationRetryInterval < now;

        if (shouldDetectUpdates || shouldSynchronizeNewUpdates || shouldSynchronizeOldUpdates)
        {
            _logger.LogDebug("Has updates to detect = {HasUpdatesToDetect} : {ShouldDetectUpdates}", hasUpdatesToDetect, shouldDetectUpdates);
            _logger.LogDebug("Has new updates to sync = {HasNewUpdatesToSync} : {ShouldSyncNewUpdates}", hasNewUpdatesToSynchronize, shouldSynchronizeNewUpdates);
            _logger.LogDebug("Has old updates to sync = {HasOldUpdatesToSync} : {ShouldSyncOldUpdates}", hasNewUpdatesToSynchronize, shouldSynchronizeNewUpdates);

            Synchronize();
        }
    }

    private void OnUpdateStatisticsTimerTick(object? sender, EventArgs e)
    {
        if (Status is SyncStatus.DetectingUpdates or SyncStatus.Synchronizing)
        {
            UpdateExecutionStatistics();
        }
    }

    private void OnStatusChanged(SyncStatus value)
    {
        StatusChanged?.Invoke(this, value);

        if (value is not (SyncStatus.Idle or SyncStatus.Paused or SyncStatus.DetectingUpdates or SyncStatus.Synchronizing))
        {
            ClearExecutionStatistics();
        }

        if (value is SyncStatus.Paused)
        {
            _localAdapter.Reset();
            _remoteAdapter.Reset();
        }
    }

    private void ClearExecutionStatistics()
    {
        OnStatisticsChanged(IExecutionStatistics.Zero);
    }

    private void ResetExecutionStatistics()
    {
        _initialExecutionStatistics = GetExecutionStatistics().ClearFailures();
        _globalFailureStatistics.ClearFailures();
    }

    private void UpdateExecutionStatistics()
    {
        var statistics = GetExecutionStatistics() - _initialExecutionStatistics + _globalFailureStatistics;
        if (!statistics.Equals(_executionStatistics))
        {
            _executionStatistics = statistics;
            OnStatisticsChanged(statistics);
        }
    }

    private IExecutionStatistics GetExecutionStatistics()
    {
        return _remoteAdapter.ExecutionStatistics +
               _localAdapter.ExecutionStatistics +
               _syncEngine.ExecutionStatistics;
    }

    private void OnDatabaseFaulted(object? sender, EventArgs e)
    {
        _isFaulty = true;
        _synchronization.Cancel();
    }

    private void OnStatisticsChanged(IExecutionStatistics value)
    {
        StatisticsChanged?.Invoke(this, value);
    }

    private void OnRemoteAdapterSyncActivityChanged(object? sender, SyncActivityChangedEventArgs<long> e)
    {
        OnSyncActivityChanged(new SyncActivityChangedEventArgs<long>(e.Item.WithReplica(Replica.Remote)));
    }

    private void OnLocalAdapterSyncActivityChanged(object? sender, SyncActivityChangedEventArgs<long> e)
    {
        OnSyncActivityChanged(new SyncActivityChangedEventArgs<long>(e.Item.WithReplica(Replica.Local)));
    }

    private void OnSyncActivityChanged(SyncActivityChangedEventArgs<long> e)
    {
        SyncActivityChanged?.Invoke(this, e);
    }

    private bool HandleConnectException(Exception ex)
    {
        if (ex is FileSystemClientException)
        {
            _logger.LogError("Failed to connect adapter: {ErrorMessage}", ex.CombinedMessage());
            return true;
        }

        return false;
    }

    private bool HandleDisconnectException(Exception ex)
    {
        if (ex is FileSystemClientException)
        {
            _logger.LogError("Failed to disconnect adapter: {ErrorMessage}", ex.CombinedMessage());
            return true;
        }

        return false;
    }
}
