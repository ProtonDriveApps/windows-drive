using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Services;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.SyncFolders;

internal sealed class SyncFolderProvider : IStoppableService, IMappingsAware, IMappingStateAware
{
    private readonly Lazy<IEnumerable<ISyncFoldersAware>> _syncFoldersAwareObjects;
    private readonly ILogger<SyncFolderProvider> _logger;

    private readonly ICollection<SyncFolder> _syncFolders = [];
    private readonly IScheduler _scheduler = new SerialScheduler();

    private volatile bool _isStopping;

    public SyncFolderProvider(
        Lazy<IEnumerable<ISyncFoldersAware>> syncFoldersAwareObjects,
        ILogger<SyncFolderProvider> logger)
    {
        _syncFoldersAwareObjects = syncFoldersAwareObjects;
        _logger = logger;
    }

    void IMappingsAware.OnMappingsChanged(
        IReadOnlyCollection<RemoteToLocalMapping> activeMappings,
        IReadOnlyCollection<RemoteToLocalMapping> deletedMappings)
    {
        Schedule(HandleMappingsChange);

        return;

        void HandleMappingsChange()
        {
            var unprocessedSyncFolders = _syncFolders.ToList();
            var newSyncFolders = new List<SyncFolder>();

            foreach (var mapping in activeMappings)
            {
                var syncFolder = _syncFolders.FirstOrDefault(s => s.Mapping == mapping);
                if (syncFolder != null)
                {
                    unprocessedSyncFolders.Remove(syncFolder);
                    OnSyncFolderChanged(SyncFolderChangeType.Updated, syncFolder);

                    continue;
                }

                newSyncFolders.Add(new SyncFolder(mapping));
            }

            foreach (var syncFolder in unprocessedSyncFolders)
            {
                RemoveSyncFolder(syncFolder);
            }

            foreach (var syncFolder in newSyncFolders)
            {
                AddSyncFolder(syncFolder);
            }
        }
    }

    void IMappingStateAware.OnMappingStateChanged(RemoteToLocalMapping mapping, MappingState state)
    {
        Schedule(HandleMappingStateChange);

        return;

        void HandleMappingStateChange()
        {
            var syncFolder = _syncFolders.FirstOrDefault(s => s.Mapping == mapping);

            if (syncFolder?.SetState(state) ?? false)
            {
                OnSyncFolderChanged(SyncFolderChangeType.Updated, syncFolder);
            }

            if (state.Status is MappingSetupStatus.Failed)
            {
                FailChildSyncFolders();
            }
        }

        void FailChildSyncFolders()
        {
            if (mapping.Type is not MappingType.SharedWithMeRootFolder)
            {
                return;
            }

            foreach (var childSyncFolder in _syncFolders.Where(s => s.Type is SyncFolderType.SharedWithMeItem))
            {
                if (childSyncFolder.SetState(state))
                {
                    OnSyncFolderChanged(SyncFolderChangeType.Updated, childSyncFolder);
                }
            }
        }
    }

    async Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug($"{nameof(SyncFolderProvider)} is stopping");
        _isStopping = true;

        await WaitForCompletionAsync().ConfigureAwait(false);

        _logger.LogDebug($"{nameof(SyncFolderProvider)} stopped");
    }

    internal Task WaitForCompletionAsync()
    {
        // Wait for all previously scheduled internal tasks to complete
        return _scheduler.Schedule(() => { });
    }

    private void AddSyncFolder(SyncFolder syncFolder)
    {
        _syncFolders.Add(syncFolder);
        OnSyncFolderChanged(SyncFolderChangeType.Added, syncFolder);
    }

    private void RemoveSyncFolder(SyncFolder syncFolder)
    {
        _syncFolders.Remove(syncFolder);
        OnSyncFolderChanged(SyncFolderChangeType.Removed, syncFolder);
    }

    private void OnSyncFolderChanged(SyncFolderChangeType changeType, SyncFolder syncFolder)
    {
        if (_isStopping)
        {
            return;
        }

        foreach (var listener in _syncFoldersAwareObjects.Value)
        {
            listener.OnSyncFolderChanged(changeType, syncFolder);
        }
    }

    private void Schedule(Action action)
    {
        if (_isStopping)
        {
            return;
        }

        _scheduler.Schedule(action);
    }
}
