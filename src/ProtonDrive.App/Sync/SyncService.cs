using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Threading;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Services;
using ProtonDrive.App.Settings;
using ProtonDrive.Shared.IO;
using ProtonDrive.Shared.Offline;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Shared.ExecutionStatistics;
using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.App.Sync;

internal class SyncService
    : ISyncService, IStoppableService, ISessionStateAware, IMappingsSetupStateAware, IOfflineStateAware, IRemoteIdsFromLocalPathProvider, ISyncRootPathProvider
{
    private readonly SyncAgentFactory _syncAgentFactory;
    private readonly IRepository<SyncSettings> _settingsRepository;
    private readonly IOfflineService _offlineService;
    private readonly Lazy<IEnumerable<ISyncStateAware>> _syncStateAware;
    private readonly Lazy<IEnumerable<ISyncStatisticsAware>> _syncStatisticsAware;
    private readonly Lazy<IEnumerable<ISyncActivityAware>> _syncActivityAware;
    private readonly IFileSystemIdentityProvider<long> _fileSystemIdentityProvider;
    private readonly ILogger<SyncService> _logger;

    private readonly CancellationHandle _cancellationHandle = new();
    private readonly AsyncManualResetEvent _syncAgentAvailabilityEvent = new(initialState: false);
    private readonly IScheduler _scheduler;
    private readonly CoalescingAction _processStateChange;
    private readonly CoalescingAction _processPausing;

    private volatile bool _stopping;
    private volatile MappingsSetupState _mappingsSetupState = MappingsSetupState.None;
    private SyncServiceStatus _serviceStatus;
    private SyncStatus _syncAgentStatus;
    private SyncStatus _status;
    private bool _syncFailed;
    private bool _offline;
    private SyncAgent? _syncAgent;
    private IReadOnlyCollection<RemoteToLocalMapping> _syncedMappings = [];
    private IReadOnlyCollection<RemoteToLocalMapping> _syncedSucceededMappings = [];

    public SyncService(
        SyncAgentFactory syncAgentFactory,
        IRepository<SyncSettings> settingsRepository,
        IOfflineService offlineService,
        Lazy<IEnumerable<ISyncStateAware>> syncStateAware,
        Lazy<IEnumerable<ISyncStatisticsAware>> syncStatisticsAware,
        Lazy<IEnumerable<ISyncActivityAware>> syncActivityAware,
        IFileSystemIdentityProvider<long> fileSystemIdentityProvider,
        ILogger<SyncService> logger)
    {
        _syncAgentFactory = syncAgentFactory;
        _settingsRepository = settingsRepository;
        _offlineService = offlineService;
        _syncStateAware = syncStateAware;
        _syncStatisticsAware = syncStatisticsAware;
        _syncActivityAware = syncActivityAware;
        _fileSystemIdentityProvider = fileSystemIdentityProvider;
        _logger = logger;

        _processStateChange = new CoalescingAction(InternalProcessStateChange);
        _processPausing = new CoalescingAction(InternalProcessPausing);

        _scheduler =
            new HandlingCancellationSchedulerDecorator(
                nameof(SyncService),
                logger,
                new LoggingExceptionsSchedulerDecorator(
                    nameof(SyncService),
                    logger,
                    new SerialScheduler()));
    }

    public SyncStatus Status
    {
        get => _status;
        private set
        {
            if (_status != value)
            {
                _status = value;
                OnStateChanged(value, _syncFailed);
            }
        }
    }

    public bool Paused
    {
        get => _settingsRepository.Get()?.Paused == true;
        set
        {
            var settings = _settingsRepository.Get() ?? new SyncSettings();
            settings.Paused = value;

            var syncAgent = _syncAgent;
            if (syncAgent != null && !value)
            {
                ForceOnline();
            }

            _settingsRepository.Set(settings);
            _logger.LogInformation($"The user {(value ? "paused" : "restarted")} the synchronization");

            ProcessPausing();
            ProcessStateChange();
        }
    }

    protected SyncServiceStatus ServiceStatus
    {
        get => _serviceStatus;
        private set
        {
            if (_serviceStatus != value)
            {
                _serviceStatus = value;
                ProcessStateChange();
            }
        }
    }

    async Task<RemoteIds?> IRemoteIdsFromLocalPathProvider.GetRemoteIdsOrDefaultAsync(string localPath, CancellationToken cancellationToken)
    {
        await _syncAgentAvailabilityEvent.WaitAsync(cancellationToken).ConfigureAwait(false);

        return await Schedule<RemoteIds?>(
            async ct =>
            {
                if (_syncAgent is null || !_syncAgentAvailabilityEvent.IsSet)
                {
                    return null;
                }

                var mapping = _syncedMappings.FirstOrDefault(m => PathComparison.IsAncestor(m.Local.RootFolderPath, localPath));

                if (mapping is not { Remote: { VolumeId: not null, ShareId: not null } })
                {
                    return default;
                }

                if (!_fileSystemIdentityProvider.TryGetIdFromPath(localPath, out var fileId))
                {
                    return default;
                }

                var linkId = await _syncAgent.GetRemoteIdFromAltIdOrDefaultAsync((mapping.Local.InternalVolumeId, fileId), ct)
                    .ConfigureAwait(false);

                return linkId?.ItemId is not null ? new RemoteIds(mapping.Remote.VolumeId, mapping.Remote.ShareId, linkId.Value.ItemId) : null;
            }).ConfigureAwait(false);
    }

    IReadOnlyList<string> ISyncRootPathProvider.GetOfTypes(IReadOnlyCollection<MappingType> types)
    {
        return _mappingsSetupState.Mappings
            .Where(mapping => mapping.Status == MappingStatus.Complete && types.Contains(mapping.Type))
            .Select(mapping => mapping.Local.RootFolderPath)
            .ToList()
            .AsReadOnly();
    }

    public void Synchronize()
    {
        if (ServiceStatus == SyncServiceStatus.Started)
        {
            _syncAgent?.Synchronize();
        }
    }

    public async Task RestartAsync()
    {
        await Schedule(InternalStopAsync).ConfigureAwait(false);
        await Schedule(InternalStartAsync).ConfigureAwait(false);
    }

    async Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{nameof(SyncService)} is stopping");
        _stopping = true;
        _cancellationHandle.Cancel();

        if (ServiceStatus is not SyncServiceStatus.Stopped)
        {
            _logger.LogDebug("Scheduling synchronization stop");
        }

        await Schedule(InternalStopAsync).ConfigureAwait(false);

        _logger.LogInformation($"{nameof(SyncService)} stopped");
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        if (_stopping || value.Status == SessionStatus.Started)
        {
            return;
        }

        if (ServiceStatus is SyncServiceStatus.Starting or SyncServiceStatus.Started or SyncServiceStatus.Failed)
        {
            _logger.LogDebug("Session status is {Status}, scheduling synchronization stop", value.Status);

            _cancellationHandle.Cancel();
            Schedule(InternalStopAsync);
        }
    }

    void IMappingsSetupStateAware.OnMappingsSetupStateChanged(MappingsSetupState value)
    {
        _mappingsSetupState = value;

        if (value.Status is not MappingSetupStatus.SettingUp)
        {
            ProcessMappingsStateChange(_mappingsSetupState);
        }
    }

    async Task IMappingsSetupStateAware.OnMappingsSettingUpAsync()
    {
        if (ProcessMappingsStateChange(_mappingsSetupState))
        {
            _logger.LogInformation("Waiting for synchronization to stop");
            await WaitForCompletionAsync().ConfigureAwait(false);
            _logger.LogInformation("Completed waiting for synchronization to stop");
        }
    }

    void IOfflineStateAware.OnOfflineStateChanged(OfflineStatus status)
    {
        _offline = status != OfflineStatus.Online;

        ProcessPausing();
        ProcessStateChange();
    }

    private Task WaitForCompletionAsync()
    {
        // Wait for scheduled tasks to complete
        return _scheduler.Schedule(() => { });
    }

    private bool ProcessMappingsStateChange(MappingsSetupState value)
    {
        if (_stopping)
        {
            return false;
        }

        if (ServiceStatus is SyncServiceStatus.Stopped)
        {
            if (value.Status is not (MappingSetupStatus.Succeeded or MappingSetupStatus.PartiallySucceeded))
            {
                return false;
            }

            _logger.LogDebug("Scheduling synchronization start");

            Schedule(InternalStartAsync);

            return false;
        }

        switch (value.Status)
        {
            case MappingSetupStatus.SettingUp:
                // If mappings, that were used for syncing, are removed, the SyncService should stop
                // to allow MappingSetupService to tier down those mappings.
                var hasRemovedMappings = _syncedMappings.Except(value.Mappings).Any();
                if (hasRemovedMappings)
                {
                    _logger.LogInformation("Mappings removed, scheduling synchronization stop");

                    _cancellationHandle.Cancel();
                    Schedule(InternalStopAsync);

                    // The sync should stop before the mapping setup proceeds
                    return true;
                }

                // If mappings, that previously were successfully setup, are not anymore, the SyncService should
                // stop and wait for the MappingSetupService to set them up.
                var hasNotSucceededMappings = _syncedSucceededMappings.Except(value.Mappings.Where(m => m.HasSetupSucceeded)).Any();
                if (hasNotSucceededMappings)
                {
                    _logger.LogInformation("Mappings being set up, scheduling synchronization stop");

                    _cancellationHandle.Cancel();
                    Schedule(InternalStopAsync);

                    // The mapping setup can proceed while the sync is stopping
                    return false;
                }

                _logger.LogInformation("No mappings removed, continuing synchronization");

                break;

            case MappingSetupStatus.Succeeded or MappingSetupStatus.PartiallySucceeded:
                // Checking whether the SyncService should restart to sync newly added or successfully set up mappings
                var hasNewMappingsToSync = value.Mappings.Where(m => m.HasSetupSucceeded).Except(_syncedSucceededMappings).Any();
                if (hasNewMappingsToSync || ServiceStatus is SyncServiceStatus.Stopping or SyncServiceStatus.Failed)
                {
                    _logger.LogInformation("New mappings set up, scheduling synchronization restart");

                    _cancellationHandle.Cancel();
                    Schedule(InternalStopAsync);
                    Schedule(InternalStartAsync);
                }
                else
                {
                    _logger.LogInformation("No new mappings set up, continuing synchronization");
                }

                break;

            default:
                _logger.LogDebug("Mappings setup status is {Status}, scheduling synchronization stop", value.Status);
                _cancellationHandle.Cancel();
                Schedule(InternalStopAsync);

                break;
        }

        return false;
    }

    private async Task InternalStartAsync(CancellationToken cancellationToken)
    {
        if (_stopping)
        {
            return;
        }

        if (ServiceStatus != SyncServiceStatus.Stopped)
        {
            _logger.LogDebug("Synchronization is running, skipping synchronization start");
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var mappingsSetupState = _mappingsSetupState;

        if (mappingsSetupState.Status is not (MappingSetupStatus.Succeeded or MappingSetupStatus.PartiallySucceeded))
        {
            _logger.LogWarning("Mappings setup has not succeeded, skipping synchronization start");
            return;
        }

        _logger.LogInformation("Starting synchronization");
        ServiceStatus = SyncServiceStatus.Starting;

        var syncAgent = await CreateSyncAgentAsync(mappingsSetupState.Mappings, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var succeeded = await syncAgent.StartAsync(cancellationToken).ConfigureAwait(false);

        if (!succeeded)
        {
            _logger.LogError("Failed to start synchronization");
            ServiceStatus = SyncServiceStatus.Failed;

            await syncAgent.StopAsync().ConfigureAwait(false);

            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Synchronization started");
        ServiceStatus = SyncServiceStatus.Started;
    }

    private async Task InternalStopAsync(CancellationToken cancellationToken)
    {
        if (ServiceStatus == SyncServiceStatus.Stopped)
        {
            _logger.LogDebug("Synchronization is already stopped");
            return;
        }

        _syncAgentAvailabilityEvent.Reset();

        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Stopping synchronization");
        ServiceStatus = SyncServiceStatus.Stopping;

        var syncStopTask = _syncAgent?.StopAsync();

        ServiceStatus = SyncServiceStatus.Stopping;

        if (syncStopTask != null)
        {
            var succeeded = await syncStopTask.ConfigureAwait(false);

            if (!succeeded)
            {
                _logger.LogError("Failed to stop synchronization");
                ServiceStatus = SyncServiceStatus.Failed;

                return;
            }
        }

        DisposeSyncAgent();

        _logger.LogInformation("Synchronization stopped");
        ServiceStatus = SyncServiceStatus.Stopped;

        cancellationToken.ThrowIfCancellationRequested();

        // Try to start service in case folder mapping status has changed to Succeeded
        // while stop action was running.
        ProcessMappingsStateChange(_mappingsSetupState);
    }

    private void ProcessPausing()
    {
        _processPausing.Run();
    }

    private void InternalProcessPausing()
    {
        if (_stopping)
        {
            return;
        }

        var syncAgent = _syncAgent;
        if (syncAgent != null)
        {
            // SyncAgent can be paused because of two reasons:
            // the synchronization is paused or the app is offline.
            syncAgent.Paused = Paused || _offline;
        }
    }

    private void OnSyncAgentStatusChanged(object? sender, SyncStatus value)
    {
        _syncAgentStatus = value;

        ProcessStateChange();
    }

    private void ProcessStateChange()
    {
        _processStateChange.Run();
    }

    private void InternalProcessStateChange()
    {
        if (_stopping)
        {
            return;
        }

        var syncAgentStatus = _syncAgentStatus;

        Status = _serviceStatus switch
        {
            SyncServiceStatus.Stopped => SyncStatus.Terminated,
            SyncServiceStatus.Starting => SyncStatus.Initializing,
            SyncServiceStatus.Started => syncAgentStatus switch
            {
                // SyncAgent can be paused because of two reasons: the synchronization is paused
                // or the app is offline. Need to distinguish between the two. If the pausing
                // reason is offline mode, we do not set the status to Paused.
                SyncStatus.Paused => !Paused && _offline ? SyncStatus.Offline : SyncStatus.Paused,
                _ => syncAgentStatus,
            },
            SyncServiceStatus.Stopping => SyncStatus.Terminating,
            SyncServiceStatus.Failed => SyncStatus.Failed,
            _ => throw new NotSupportedException(),
        };
    }

    private void OnStateChanged(SyncStatus status, bool failed)
    {
        var state = new SyncState(status, failed);

        foreach (var listener in _syncStateAware.Value)
        {
            listener.OnSyncStateChanged(state);
        }
    }

    private void OnSyncAgentStatisticsChanged(object? sender, IExecutionStatistics value)
    {
        OnStatisticsChanged(value);
    }

    private void OnStatisticsChanged(IExecutionStatistics value)
    {
        _syncFailed = value.Failed != 0;

        foreach (var listener in _syncStatisticsAware.Value)
        {
            listener.OnSyncStatisticsChanged(value);
        }
    }

    private void OnSyncAgentSyncActivityChanged(object? sender, SyncActivityChangedEventArgs<long> e)
    {
        OnSyncActivityChanged(e.Item);
    }

    private void OnSyncActivityChanged(SyncActivityItem<long> item)
    {
        foreach (var listener in _syncActivityAware.Value)
        {
            listener.OnSyncActivityChanged(item);
        }
    }

    private void OnSyncAgentAvailabilityChanged(object? sender, bool isAvailable)
    {
        if (isAvailable)
        {
            _syncAgentAvailabilityEvent.Set();
        }
        else
        {
            _syncAgentAvailabilityEvent.Reset();
        }
    }

    private void ForceOnline()
    {
        if (_stopping)
        {
            return;
        }

        _offlineService.ForceOnline();
    }

    private async Task<SyncAgent> CreateSyncAgentAsync(IReadOnlyCollection<RemoteToLocalMapping> mappings, CancellationToken cancellationToken)
    {
        var syncAgent = await _syncAgentFactory.GetSyncAgentAsync(mappings, cancellationToken).ConfigureAwait(false);

        syncAgent.AvailabilityChanged += OnSyncAgentAvailabilityChanged;
        syncAgent.StatusChanged += OnSyncAgentStatusChanged;
        syncAgent.StatisticsChanged += OnSyncAgentStatisticsChanged;
        syncAgent.SyncActivityChanged += OnSyncAgentSyncActivityChanged;

        _syncedMappings = mappings;
        _syncedSucceededMappings = mappings.Where(m => m.HasSetupSucceeded).ToArray();
        _syncAgent = syncAgent;

        syncAgent.Paused = Paused;

        return syncAgent;
    }

    private void DisposeSyncAgent()
    {
        if (_syncAgent == null)
        {
            return;
        }

        _syncAgent.AvailabilityChanged -= OnSyncAgentAvailabilityChanged;

        _syncAgent.Dispose();
        _syncAgent = null;
        _syncedMappings = Array.Empty<RemoteToLocalMapping>();
        _syncedSucceededMappings = Array.Empty<RemoteToLocalMapping>();
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    private Task Schedule(Func<CancellationToken, Task> action)
    {
        var cancellationToken = _cancellationHandle.Token;

        return _scheduler.Schedule(() => action(cancellationToken), cancellationToken);
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    private Task<TResult> Schedule<TResult>(Func<CancellationToken, Task<TResult>> function)
    {
        var cancellationToken = _cancellationHandle.Token;

        return _scheduler.Schedule(() => function(cancellationToken), cancellationToken);
    }
}
