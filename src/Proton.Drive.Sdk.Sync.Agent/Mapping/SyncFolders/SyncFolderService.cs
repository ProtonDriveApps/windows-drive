using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Logging;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.SyncFolders;

internal sealed class SyncFolderService : ISyncFolderService, IMappingsAware
{
    private readonly AppConfig _appConfig;
    private readonly IMappingRegistry _mappingRegistry;
    private readonly ILocalSyncFolderValidator _localSyncFolderValidator;
    private readonly ILogger<SyncFolderService> _logger;

    private IReadOnlyCollection<RemoteToLocalMapping> _activeMappings = [];

    public SyncFolderService(
        AppConfig appConfig,
        IMappingRegistry mappingRegistry,
        ILocalSyncFolderValidator localSyncFolderValidator,
        ILogger<SyncFolderService> logger)
    {
        _appConfig = appConfig;
        _mappingRegistry = mappingRegistry;
        _localSyncFolderValidator = localSyncFolderValidator;
        _logger = logger;
    }

    public SyncFolderValidationResult ValidateAccountRootFolder(string path)
    {
        var allPaths = GetSyncedFolderPaths().ToHashSet();

        return
            _localSyncFolderValidator.ValidateDrive(path) ??
            _localSyncFolderValidator.ValidatePath(path, allPaths) ??
            _localSyncFolderValidator.ValidateFolder(path, shouldBeEmpty: true) ??
            SyncFolderValidationResult.Succeeded;
    }

    public SyncFolderValidationResult ValidateSyncFolder(string path, IEnumerable<string> otherPaths)
    {
        var allPaths = GetSyncedFolderPaths().Concat(otherPaths).ToHashSet();

        return
            _localSyncFolderValidator.ValidateDrive(path) ??
            _localSyncFolderValidator.ValidatePath(path, allPaths) ??
            _localSyncFolderValidator.ValidateFolder(path, shouldBeEmpty: false) ??
            SyncFolderValidationResult.Succeeded;
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
                Path = cloudFilesFolderPath,
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

            if (activeMappings.Any(x => x.Type == MappingType.HostDeviceFolder && x.Local.Path.Equals(localPath)))
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
                    Path = localPath,
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
            "Requested to remove host device sync folder \"{Path}\", mapping {MappingId}",
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

    public async Task SetStorageOptimizationAsync(SyncFolder syncFolder, bool isEnabled, CancellationToken cancellationToken)
    {
        Ensure.IsTrue(
            syncFolder.Type is SyncFolderType.HostDeviceFolder,
            $"Sync folder type must be {SyncFolderType.HostDeviceFolder}",
            nameof(syncFolder));

        using var mappings = await _mappingRegistry.GetMappingsAsync(cancellationToken).ConfigureAwait(false);

        var pathToLog = _logger.GetSensitiveValueForLogging(syncFolder.LocalPath);

        LogRequest();

        if (syncFolder.Status is not MappingSetupStatus.Succeeded)
        {
            LogMappingSetupStatus();
            return;
        }

        var mapping = mappings.GetActive().FirstOrDefault(m => m == syncFolder.Mapping);

        if (mapping is null)
        {
            LogMappingIsMissing();
            return;
        }

        if (mapping.Status is not MappingStatus.Complete)
        {
            LogMappingStatus();
            return;
        }

        mapping.Local.StorageOptimization ??= new StorageOptimizationState();
        mapping.Local.StorageOptimization.IsEnabled = isEnabled;
        mapping.Local.StorageOptimization.Status = StorageOptimizationStatus.Pending;
        mapping.IsDirty = true;

        mappings.Update(mapping);

        return;

        void LogRequest()
        {
            _logger.LogInformation(
                "Requested to {Action} storage optimization for host device sync folder \"{Path}\", mapping {MappingId}",
                GetStorageOptimizationActionName(),
                pathToLog,
                syncFolder.Mapping.Id);
        }

        string GetStorageOptimizationActionName()
        {
            return isEnabled switch
            {
                false => "disable",
                true => "enable",
            };
        }

        void LogMappingSetupStatus()
        {
            _logger.LogWarning(
                "Unable to set storage optimization for host device sync folder \"{Path}\", mapping setup status is {SetupStatus}",
                pathToLog,
                syncFolder.Status);
        }

        void LogMappingIsMissing()
        {
            _logger.LogWarning("Unable to locate mapping for host device sync folder \"{Path}\"", pathToLog);
        }

        void LogMappingStatus()
        {
            _logger.LogWarning(
                "Unable to enable on-demand sync for host device sync folder \"{Path}\", mapping status is {MappingStatus}",
                pathToLog,
                mapping.Status);
        }
    }

    void IMappingsAware.OnMappingsChanged(
        IReadOnlyCollection<RemoteToLocalMapping> activeMappings,
        IReadOnlyCollection<RemoteToLocalMapping> deletedMappings)
    {
        _activeMappings = activeMappings;
    }

    private IEnumerable<string> GetSyncedFolderPaths()
    {
        // We don't use _syncFolders collection here, because access to it must be scheduled.
        // Shared with me items are skipped, it is enough to include the shared with me root folder.
        return _activeMappings
            .Where(x => x.Type is not MappingType.SharedWithMeItem)
            .Select(x => x.Local.Path);
    }
}
