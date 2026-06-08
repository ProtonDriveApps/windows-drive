using Microsoft.Extensions.Logging;
using ProtonDrive.App.Settings;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Sync.Shared.FileSystem.Integration;

namespace ProtonDrive.App.Mapping.Setup;

internal sealed class LocalStorageOptimizationStep : ILocalStorageOptimizationStep
{
    private readonly AppConfig _appConfig;
    private readonly ILocalFolderService _localFolderService;
    private readonly ILogger<LocalStorageOptimizationStep> _logger;

    public LocalStorageOptimizationStep(
        AppConfig appConfig,
        ILocalFolderService localFolderService,
        ILogger<LocalStorageOptimizationStep> logger)
    {
        _appConfig = appConfig;
        _localFolderService = localFolderService;
        _logger = logger;
    }

    /// <summary>
    /// Turning storage optimization on removes file pinning to automatically free up space used by file content, whenever needed.
    /// Turning storage optimization off pins files to always be kept on this device.
    /// </summary>
    public StorageOptimizationErrorCode? Execute(RemoteToLocalMapping mapping)
    {
        Ensure.IsTrue(mapping.SyncMethod is SyncMethod.OnDemand, $"Sync method is not {nameof(SyncMethod.OnDemand)}", nameof(mapping.SyncMethod));

        if (!mapping.IsStorageOptimizationPending())
        {
            return null;
        }

        var path = mapping.Local.Path;
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Local folder path must be specified");
        }

        var pinState = mapping.Local.StorageOptimization?.IsEnabled == true
            ? FilePinState.Unspecified
            : FilePinState.Pinned;

        var succeeded = _localFolderService.TrySetPinState(path, pinState);

        StorageOptimizationErrorCode? result = null;

        if (!succeeded && (!_localFolderService.TryGetPinState(path, out var actualPinState) || actualPinState != pinState))
        {
            _logger.LogWarning("Storage optimization failed");
            result = StorageOptimizationErrorCode.Unknown;
        }
        else
        {
            _logger.LogInformation("Storage optimization succeeded");
        }

        TryMarkSpecialSubfoldersExcludedFromSync(mapping.Local.Path);

        return result;
    }

    private void TryMarkSpecialSubfoldersExcludedFromSync(string rootPath)
    {
        TryMarkFolderExcludedFromSync(rootPath, _appConfig.FolderNames.TempFolderName);
        TryMarkFolderExcludedFromSync(rootPath, _appConfig.FolderNames.BackupFolderName);
    }

    private void TryMarkFolderExcludedFromSync(string rootPath, string relativePath)
    {
        var path = Path.Combine(rootPath, relativePath);

        _localFolderService.TrySetPinState(path, FilePinState.Excluded);
    }
}
