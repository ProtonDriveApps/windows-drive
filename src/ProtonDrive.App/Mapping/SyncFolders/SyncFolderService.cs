using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Settings;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Mapping.SyncFolders;

internal sealed class SyncFolderService : ISyncFolderService, IMappingsAware, IMappingStateAware
{
    private readonly AppConfig _appConfig;
    private readonly IMappingRegistry _mappingRegistry;
    private readonly ILocalSyncFolderValidator _localSyncFolderValidator;
    private readonly Lazy<IEnumerable<ISyncFoldersAware>> _syncFoldersAware;
    private readonly ILogger<SyncFolderService> _logger;

    private readonly ICollection<SyncFolder> _syncFolders = [];
    private readonly IScheduler _scheduler = new SerialScheduler();

    public SyncFolderService(
        AppConfig appConfig,
        IMappingRegistry mappingRegistry,
        ILocalSyncFolderValidator localSyncFolderValidator,
        Lazy<IEnumerable<ISyncFoldersAware>> syncFoldersAware,
        ILogger<SyncFolderService> logger)
    {
        _appConfig = appConfig;
        _mappingRegistry = mappingRegistry;
        _localSyncFolderValidator = localSyncFolderValidator;
        _syncFoldersAware = syncFoldersAware;
        _logger = logger;
    }

    public SyncFolderValidationResult ValidateAccountRootFolder(string path)
    {
        var pathValidationResult = _localSyncFolderValidator.ValidatePath(path, new HashSet<string>(0));

        if (pathValidationResult is not SyncFolderValidationResult.Succeeded)
        {
            return pathValidationResult;
        }

        return _localSyncFolderValidator.ValidateFolder(path, shouldBeEmpty: true);
    }

    public SyncFolderValidationResult ValidateSyncFolder(string path, IReadOnlySet<string> otherPaths)
    {
        var pathValidationResult = _localSyncFolderValidator.ValidatePathAndDrive(path, otherPaths);

        if (pathValidationResult is not SyncFolderValidationResult.Succeeded)
        {
            return pathValidationResult;
        }

        return _localSyncFolderValidator.ValidateFolder(path, shouldBeEmpty: false);
    }

    public async Task SetAccountRootFolderAsync(string localPath)
    {
        Ensure.NotNullOrEmpty(localPath, nameof(localPath));

        var pathToLog = _logger.GetSensitiveValueForLogging(localPath);
        _logger.LogInformation("Requested to change account root folder to \"{Path}\"", pathToLog);

        using var mappings = await _mappingRegistry.GetMappingsAsync(CancellationToken.None).ConfigureAwait(false);

        var previousMapping = mappings.GetActive().FirstOrDefault(m => m.Type is MappingType.CloudFiles);

        if (previousMapping != null)
        {
            mappings.Delete(previousMapping);
        }

        var cloudFilesFolderPath = Path.Combine(localPath, _appConfig.FolderNames.CloudFilesFolderName);

        var newMapping = new RemoteToLocalMapping
        {
            Type = MappingType.CloudFiles,
            SyncMethod = SyncMethod.OnDemand,
            Local =
            {
                RootFolderPath = cloudFilesFolderPath,
            },
        };

        mappings.Add(newMapping);
    }

    public async Task AddHostDeviceFoldersAsync(ICollection<string> localPaths, CancellationToken cancellationToken)
    {
        Ensure.IsTrue(localPaths.All(p => !string.IsNullOrEmpty(p)), "Local path must be not empty", nameof(localPaths));

        using var mappings = await _mappingRegistry.GetMappingsAsync(cancellationToken).ConfigureAwait(false);

        var activeMappings = mappings.GetActive();

        foreach (var localPath in localPaths)
        {
            var pathToLog = _logger.GetSensitiveValueForLogging(localPath);
            _logger.LogInformation("Requested to add host device sync folder \"{Path}\"", pathToLog);

            if (activeMappings.Any(x => x.Type == MappingType.HostDeviceFolder && x.Local.RootFolderPath.Equals(localPath)))
            {
                _logger.LogWarning("Ignored sync folder \"{Path}\", since it is already mapped", pathToLog);

                continue;
            }

            var newMapping = new RemoteToLocalMapping
            {
                Type = MappingType.HostDeviceFolder,
                SyncMethod = SyncMethod.Classic,
                Local =
                {
                    RootFolderPath = localPath,
                },
            };

            mappings.Add(newMapping);
        }
    }

    public async Task RemoveHostDeviceFolderAsync(SyncFolder syncFolder, CancellationToken cancellationToken)
    {
        Ensure.IsTrue(
            syncFolder.Type is SyncFolderType.HostDeviceFolder,
            $"Sync folder type must be {SyncFolderType.HostDeviceFolder}",
            nameof(syncFolder));

        using var mappings = await _mappingRegistry.GetMappingsAsync(cancellationToken).ConfigureAwait(false);

        var pathToLog = _logger.GetSensitiveValueForLogging(syncFolder.LocalPath);
        _logger.LogInformation(
            "Requested to remove host device sync folder \"{Path}\", mapping ID={MappingId}",
            pathToLog,
            syncFolder.Mapping.Id);

        var mapping = mappings.GetActive().FirstOrDefault(m => m == syncFolder.Mapping);

        if (mapping is null)
        {
            _logger.LogWarning("Unable to find mapping for host device sync folder \"{Path}\"", pathToLog);

            return;
        }

        mappings.Delete(mapping);
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

    internal Task WaitForCompletionAsync()
    {
        // Wait for all previously scheduled internal tasks to complete
        return _scheduler.Schedule(() => false);
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
        foreach (var listener in _syncFoldersAware.Value)
        {
            listener.OnSyncFolderChanged(changeType, syncFolder);
        }
    }

    private void Schedule(Action action)
    {
        _scheduler.Schedule(action);
    }
}
