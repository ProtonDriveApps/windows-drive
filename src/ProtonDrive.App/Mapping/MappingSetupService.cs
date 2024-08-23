using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoreLinq;
using ProtonDrive.App.Onboarding;
using ProtonDrive.App.Services;
using ProtonDrive.App.Settings;
using ProtonDrive.App.Sync;
using ProtonDrive.App.Volumes;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.App.Mapping;

internal sealed class MappingSetupService : IMappingSetupService, IStoppableService, IMappingsAware, IVolumeStateAware, ISyncStateAware, IOnboardingStateAware, IRootDeletionHandler
{
    private readonly IMappingRegistry _mappingRegistry;
    private readonly IMappingSetupPipeline _mappingSetupPipeline;
    private readonly IMappingTeardownPipeline _mappingTeardownPipeline;
    private readonly Lazy<IEnumerable<IMappingsSetupStateAware>> _setupStateAware;
    private readonly Lazy<IEnumerable<IMappingStateAware>> _mappingStateAware;
    private readonly ILogger<MappingSetupService> _logger;
    private readonly CoalescingAction _mappingsSetup;
    private readonly List<RemoteToLocalMapping> _alreadySetUpMappings = [];
    private readonly ConcurrentQueue<int> _deletedSyncRootIds = new();

    private volatile bool _stopping;
    private VolumeState _volumeState = VolumeState.Idle;
    private SyncState _syncState = SyncState.Terminated;
    private OnboardingState _onboardingState = OnboardingState.NotStarted;

    private Mappings _mappings = new([], []);
    private Mappings _newMappings = new([], []);

    public MappingSetupService(
        IMappingRegistry mappingRegistry,
        IMappingSetupPipeline mappingSetupPipeline,
        IMappingTeardownPipeline mappingTeardownPipeline,
        Lazy<IEnumerable<IMappingsSetupStateAware>> setupStateAware,
        Lazy<IEnumerable<IMappingStateAware>> mappingStateAware,
        ILogger<MappingSetupService> logger)
    {
        _mappingRegistry = mappingRegistry;
        _mappingSetupPipeline = mappingSetupPipeline;
        _mappingTeardownPipeline = mappingTeardownPipeline;
        _setupStateAware = setupStateAware;
        _mappingStateAware = mappingStateAware;
        _logger = logger;

        _mappingsSetup = new CoalescingAction(
            cancellationToken => WithLoggedExceptions(
                () => WithSafeCancellation(
                    () => SetUpMappingsAsync(cancellationToken))));
    }

    public MappingsSetupState State { get; private set; } = MappingsSetupState.None;

    public Task SetUpMappingsAsync()
    {
        return ScheduleSetup(forceRestart: false);
    }

    async Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{nameof(MappingSetupService)} is stopping");
        _stopping = true;
        _mappingsSetup.Cancel();

        // Wait for scheduled task to complete
        await _mappingsSetup.CurrentTask.ConfigureAwait(false);

        _logger.LogInformation($"{nameof(MappingSetupService)} stopped");
    }

    void IMappingsAware.OnMappingsChanged(
        IReadOnlyCollection<RemoteToLocalMapping> activeMappings,
        IReadOnlyCollection<RemoteToLocalMapping> deletedMappings)
    {
        _newMappings = new Mappings(activeMappings, [.. deletedMappings]);

        ScheduleSetup(forceRestart: true);
    }

    void IVolumeStateAware.OnVolumeStateChanged(VolumeState value)
    {
        // Checking whether status changed into Succeeded from
        // a different one, or from Succeeded into a different one
        var statusChanged = _volumeState.Status != value.Status
                            && (_volumeState.Status == VolumeServiceStatus.Succeeded
                                || value.Status == VolumeServiceStatus.Succeeded);

        _volumeState = value;

        if (statusChanged)
        {
            ScheduleSetup(forceRestart: value.Status is VolumeServiceStatus.Idle);
        }
    }

    void ISyncStateAware.OnSyncStateChanged(SyncState value)
    {
        _syncState = value;
    }

    void IOnboardingStateAware.OnboardingStateChanged(OnboardingState value)
    {
        _onboardingState = value;

        ScheduleSetup(forceRestart: false);
    }

    void IRootDeletionHandler.HandleRootDeletion(IEnumerable<int> syncRootIds)
    {
        syncRootIds.ForEach(_deletedSyncRootIds.Enqueue);

        ScheduleSetup(forceRestart: true);
    }

    private static bool HasMappingSetupSucceeded(IEnumerable<RemoteToLocalMapping> activeMappings, MappingType type)
    {
        var mapping = activeMappings.FirstOrDefault(m => m.Type == type);

        return mapping?.HasSetupSucceeded == true;
    }

    private Task ScheduleSetup(bool forceRestart)
    {
        if (_stopping)
        {
            return Task.CompletedTask;
        }

        _logger.LogDebug("Scheduling sync folder mappings setup");

        if (forceRestart)
        {
            _mappingsSetup.Cancel();
        }

        return _mappingsSetup.Run();
    }

    private async Task SetUpMappingsAsync(CancellationToken cancellationToken)
    {
        if (_stopping)
        {
            return;
        }

        var mappings = _newMappings;
        var mappingsChanged = mappings != _mappings;
        if (mappingsChanged)
        {
            _mappings = mappings;
        }

        if (!ValidatePreconditions())
        {
            _logger.LogDebug("Skipping sync folder mappings setup, preconditions not valid");

            var isStarted = State.Status != MappingSetupStatus.None;
            await SetStateAsync(MappingSetupStatus.None).ConfigureAwait(false);

            if (isStarted)
            {
                ResetMappingsStatus(_mappings.Active);
                ResetMappingsStatus(_mappings.Deleted);
            }

            return;
        }

        bool hasAffectedMappings = false;

        _deletedSyncRootIds
            .Select(id => mappings.Active.FirstOrDefault(m => m.Id == id))
            .Where(mapping => mapping?.HasSetupSucceeded == true)
            .ForEach(mapping =>
                {
                    hasAffectedMappings = true;
                    mapping!.HasSetupSucceeded = false;
                    SetMappingState(mapping, MappingSetupStatus.None);
                });

        if (!hasAffectedMappings && !mappingsChanged && State.Status is MappingSetupStatus.Succeeded)
        {
            _logger.LogInformation("Skipping sync folder mappings setup, mappings didn't change");

            return;
        }

        if (!ValidateMappingCreationOrder(mappings.Active))
        {
            _logger.LogInformation("Skipping sync folder mappings setup, mappings not consistent");

            var isStarted = State.Status != MappingSetupStatus.None;
            await SetStateAsync(MappingSetupStatus.None).ConfigureAwait(false);

            if (isStarted)
            {
                ResetMappingsStatus(_mappings.Active);
                ResetMappingsStatus(_mappings.Deleted);
            }

            return;
        }

        if (State.Status is MappingSetupStatus.Succeeded or MappingSetupStatus.PartiallySucceeded)
        {
            if (mappingsChanged)
            {
                _logger.LogInformation("Restarting sync folder mappings setup, mappings changed");
            }
            else if (hasAffectedMappings)
            {
                _logger.LogInformation("Restarting sync folder mappings setup, sync roots deleted");
            }
        }

        _logger.LogInformation("Starting sync folder mappings setup");

        await SetStateAsync(MappingSetupStatus.SettingUp).ConfigureAwait(false);

        var result = await TearDownDeletedMappingsAsync(mappings.Deleted, cancellationToken).ConfigureAwait(false);

        await RemoveTornDownMappings(mappings.Deleted, cancellationToken).ConfigureAwait(false);

        if (result.Status != MappingSetupStatus.Succeeded)
        {
            _logger.LogWarning("Failed to tear down deleted sync folder mappings");
            await SetErrorAsync(result.ErrorCode).ConfigureAwait(false);

            return;
        }

        await SetUpActiveMappingsAsync(mappings.Active, cancellationToken).ConfigureAwait(false);

        var successfullySetupMappings = mappings.Active.Count(x => x.HasSetupSucceeded);

        if (mappings.Active.Any() && successfullySetupMappings == 0)
        {
            _logger.LogInformation("All sync folder mappings setup has failed: {ErrorCode}", result.ErrorCode);
            await SetErrorAsync(result.ErrorCode).ConfigureAwait(false);

            return;
        }

        _logger.LogInformation("Sync folder mappings setup has succeeded: {Succeeded}/{Total}", successfullySetupMappings, mappings.Active.Count);
        await SetSuccessAsync().ConfigureAwait(false);
    }

    private void ResetMappingsStatus(IEnumerable<RemoteToLocalMapping> mappings)
    {
        foreach (var mapping in mappings)
        {
            mapping.HasSetupSucceeded = false;
            SetMappingState(mapping, MappingSetupStatus.None);
        }
    }

    private bool ValidatePreconditions()
    {
        return _volumeState.Status is VolumeServiceStatus.Succeeded
            && _onboardingState is OnboardingState.Completed;
    }

    /// <summary>
    /// Validates that foreign device mappings were created after the cloud files mapping was created.
    /// </summary>
    /// <remarks>
    /// The cloud files mapping dictates what is the account root folder path. Foreign device mappings
    /// have to be re-created to match that path. Dependent mappings are re-created asynchronously after changing
    /// the cloud files mapping, with a small delay.
    /// </remarks>
    private bool ValidateMappingCreationOrder(IReadOnlyCollection<RemoteToLocalMapping> activeMappings)
    {
        var cloudFilesMapping = activeMappings.FirstOrDefault(m => m.Type is MappingType.CloudFiles);

        // Dependent mappings should be created after the cloud files mapping was created.
        // Mappings get monotonically increasing ID values at the creation time.
        return !activeMappings.Any(m => m.Type is MappingType.ForeignDevice && (cloudFilesMapping == null || m.Id <= cloudFilesMapping.Id));
    }

    private async Task<MappingState> TearDownDeletedMappingsAsync(
        ICollection<RemoteToLocalMapping> deletedMappings,
        CancellationToken cancellationToken)
    {
        ResetMappingsStatus(deletedMappings);

        MappingState? combinedResult = null;

        // Mappings are teared down in a specific order based on mapping type
        var mappingTypesToTearDown = new[]
        {
            MappingType.HostDeviceFolder,
            MappingType.CloudFiles,
            MappingType.ForeignDevice,
            MappingType.SharedWithMeItem,
            MappingType.SharedWithMeRootFolder,
        };

        foreach (var type in mappingTypesToTearDown)
        {
            var mappingsToTearDown = deletedMappings.Where(mapping => mapping.Type == type);

            foreach (var mapping in mappingsToTearDown.Where(x => x.Status is not MappingStatus.TornDown))
            {
                var result = await TearDownMappingAsync(mapping, cancellationToken).ConfigureAwait(false);

                if (result.Status is MappingSetupStatus.Succeeded)
                {
                    await _mappingRegistry.SaveAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    combinedResult ??= result;
                }
            }
        }

        return combinedResult ?? MappingState.Success;
    }

    private async Task<MappingState> TearDownMappingAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        SetMappingState(mapping, MappingSetupStatus.SettingUp);

        var result = await _mappingTeardownPipeline.TearDownAsync(mapping, cancellationToken).ConfigureAwait(false);

        if (result.Status != MappingSetupStatus.Succeeded)
        {
            SetMappingError(mapping, result.ErrorCode);

            return result;
        }

        SetMappingState(mapping, MappingSetupStatus.Succeeded);

        return MappingState.Success;
    }

    private async Task SetUpActiveMappingsAsync(
        IReadOnlyCollection<RemoteToLocalMapping> activeMappings,
        CancellationToken cancellationToken)
    {
        var mappings = GetMappingsToSetUp(activeMappings);

        ResetMappingsStatus(mappings);

        // Sync folder mappings are set up in a specific order based on mapping type
        var mappingTypesToSetUp = new[]
        {
            MappingType.CloudFiles,
            MappingType.HostDeviceFolder,
            MappingType.ForeignDevice,
            MappingType.SharedWithMeRootFolder,
            MappingType.SharedWithMeItem,
        };

        foreach (var type in mappingTypesToSetUp)
        {
            var mappingsToSetUp = mappings.Where(m => m.Type == type).ToList().AsReadOnly();

            if (mappingsToSetUp.Count == 0)
            {
                continue;
            }

            if (type is MappingType.SharedWithMeItem && !HasMappingSetupSucceeded(activeMappings, MappingType.SharedWithMeRootFolder))
            {
                continue;
            }

            await SetUpMappingsAsync(mappingsToSetUp, cancellationToken).ConfigureAwait(false);

            if (type is MappingType.SharedWithMeRootFolder && !HasMappingSetupSucceeded(activeMappings, type))
            {
                _logger.LogWarning("Skipping set up of shared with me mappings due to the parent folder setup failed");
            }
        }
    }

    private IReadOnlyCollection<RemoteToLocalMapping> GetMappingsToSetUp(IReadOnlyCollection<RemoteToLocalMapping> activeMappings)
    {
        _alreadySetUpMappings.Clear();

        var isSyncServiceRunning = _syncState.Status is not SyncStatus.Terminated;

        if (!isSyncServiceRunning)
        {
            // The SyncService is not running, all mappings will be set up.
            return activeMappings;
        }

        // The SyncService is running, only unsuccessfully set up mappings will be set up.
        var mappingsToSetUp = new List<RemoteToLocalMapping>(activeMappings.Count);

        foreach (var mapping in activeMappings)
        {
            if (mapping.HasSetupSucceeded)
            {
                // We track already set up mappings for the overlapping local folder detection
                _alreadySetUpMappings.Add(mapping);
            }
            else
            {
                mappingsToSetUp.Add(mapping);
            }
        }

        return mappingsToSetUp.AsReadOnly();
    }

    private async Task SetUpMappingsAsync(
        IEnumerable<RemoteToLocalMapping> activeMappings,
        CancellationToken cancellationToken)
    {
        var atLeastOneMappingWasNotCompleteBeforeSetup = false;

        try
        {
            foreach (var mapping in activeMappings)
            {
                atLeastOneMappingWasNotCompleteBeforeSetup |=
                    mapping.Status is not MappingStatus.Complete ||
                    mapping.Local.InternalVolumeId == default ||
                    mapping.Remote.InternalVolumeId == default;

                await SetUpMappingAsync(mapping, cancellationToken).ConfigureAwait(false);

                _alreadySetUpMappings.Add(mapping);
            }
        }
        finally
        {
            if (atLeastOneMappingWasNotCompleteBeforeSetup)
            {
                await _mappingRegistry.SaveAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task SetUpMappingAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        SetMappingState(mapping, MappingSetupStatus.SettingUp);

        var alreadySetUpFolders = _alreadySetUpMappings
            .Select(x => x.Local.RootFolderPath)
            .ToHashSet();

        var sharedWithMeItemsFolderMapping = _alreadySetUpMappings.FirstOrDefault(x => x.Type is MappingType.SharedWithMeRootFolder);

        // Shared with me items folder is excluded to not interfere with child sync roots
        if (sharedWithMeItemsFolderMapping is not null)
        {
            alreadySetUpFolders.Remove(sharedWithMeItemsFolderMapping.Local.RootFolderPath);
        }

        var result = await _mappingSetupPipeline
            .SetUpAsync(mapping, alreadySetUpFolders, cancellationToken)
            .ConfigureAwait(false);

        mapping.HasSetupSucceeded = result.Status == MappingSetupStatus.Succeeded;

        if (result.Status != MappingSetupStatus.Succeeded)
        {
            SetMappingError(mapping, result.ErrorCode);

            return;
        }

        SetMappingState(mapping, MappingSetupStatus.Succeeded);
    }

    private async Task RemoveTornDownMappings(IList<RemoteToLocalMapping> deletedMappings, CancellationToken cancellationToken)
    {
        if (!deletedMappings.Any(x => x.Status is MappingStatus.TornDown))
        {
            return;
        }

        _logger.LogInformation("Removing torn down mappings");

        using var mappings = await _mappingRegistry.GetMappingsAsync(cancellationToken).ConfigureAwait(false);

        for (int i = deletedMappings.Count - 1; i >= 0; i--)
        {
            var mapping = deletedMappings[i];

            if (mapping.Status is not MappingStatus.TornDown)
            {
                continue;
            }

            mappings.Remove(mapping);

            deletedMappings.RemoveAt(i);
        }
    }

    private Task SetSuccessAsync()
    {
        var isPartiallySucceeded = _mappings.Active.Any(x => !x.HasSetupSucceeded);

        return SetStateAsync(isPartiallySucceeded ? MappingSetupStatus.PartiallySucceeded : MappingSetupStatus.Succeeded);
    }

    private Task SetErrorAsync(MappingErrorCode errorCode)
    {
        var state = new MappingsSetupState(MappingSetupStatus.Failed)
        {
            ErrorCode = errorCode,
        };

        return SetStateAsync(state);
    }

    private Task SetStateAsync(MappingSetupStatus status)
    {
        if (_stopping)
        {
            return Task.CompletedTask;
        }

        var activeCompletedMappings = _mappings.Active.Where(m => m.Status == MappingStatus.Complete).ToArray();

        var state = new MappingsSetupState(status)
        {
            Mappings = activeCompletedMappings,
        };

        return SetStateAsync(state);
    }

    private async Task SetStateAsync(MappingsSetupState value)
    {
        if (_stopping)
        {
            return;
        }

        State = value;

        foreach (var listener in _setupStateAware.Value)
        {
            listener.OnMappingsSetupStateChanged(value);
        }

        if (value.Status is not MappingSetupStatus.SettingUp)
        {
            return;
        }

        foreach (var listener in _setupStateAware.Value)
        {
            await listener.OnMappingsSettingUpAsync().ConfigureAwait(false);
        }
    }

    private void SetMappingState(RemoteToLocalMapping mapping, MappingSetupStatus status)
    {
        var state = new MappingState(status);

        OnMappingStateChanged(mapping, state);
    }

    private void SetMappingError(RemoteToLocalMapping mapping, MappingErrorCode errorCode)
    {
        var state = new MappingState(MappingSetupStatus.Failed)
        {
            ErrorCode = errorCode,
        };

        OnMappingStateChanged(mapping, state);
    }

    private void OnMappingStateChanged(RemoteToLocalMapping mapping, MappingState value)
    {
        if (_stopping)
        {
            return;
        }

        foreach (var listener in _mappingStateAware.Value)
        {
            listener.OnMappingStateChanged(mapping, value);
        }
    }

    private Task WithLoggedExceptions(Func<Task> origin)
    {
        return _logger.WithLoggedException(origin, "Setting up sync folder mappings has failed", includeStackTrace: true);
    }

    private async Task WithSafeCancellation(Func<Task> origin)
    {
        try
        {
            await origin().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
            _logger.LogInformation($"{nameof(MappingSetupService)} operation was cancelled");
        }
    }

    private record Mappings(IReadOnlyCollection<RemoteToLocalMapping> Active, IList<RemoteToLocalMapping> Deleted);
}
