using System.ComponentModel;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.OnDemand;
using Proton.Drive.Shared.Configuration;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.CloudFiles;

internal sealed class CloudFilesMappingSetupFinalizationStep
{
    private readonly AppConfig _appConfig;
    private readonly ISyncFolderStructureProtector _syncFolderProtector;
    private readonly IShellSyncFolderRegistry _shellSyncFolderRegistry;
    private readonly IOnDemandSyncRootRegistry _onDemandSyncRootRegistry;
    private readonly IFolderAppearanceCustomizer _folderAppearanceCustomizer;

    private bool _isUserDataFolderAppearanceCustomized;

    public CloudFilesMappingSetupFinalizationStep(
        AppConfig appConfig,
        ISyncFolderStructureProtector syncFolderProtector,
        IShellSyncFolderRegistry shellSyncFolderRegistry,
        IOnDemandSyncRootRegistry onDemandSyncRootRegistry,
        IFolderAppearanceCustomizer folderAppearanceCustomizer)
    {
        _appConfig = appConfig;
        _syncFolderProtector = syncFolderProtector;
        _shellSyncFolderRegistry = shellSyncFolderRegistry;
        _onDemandSyncRootRegistry = onDemandSyncRootRegistry;
        _folderAppearanceCustomizer = folderAppearanceCustomizer;
    }

    public async Task<MappingErrorCode> FinishSetupAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.CloudFiles)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        TryProtectLocalFolders(mapping);

        cancellationToken.ThrowIfCancellationRequested();

        if (await TryAddShellFolderAsync(mapping).ConfigureAwait(false) is { } errorCode)
        {
            return errorCode;
        }

        cancellationToken.ThrowIfCancellationRequested();

        CustomizeUserDataFolderAppearance();

        return MappingErrorCode.None;
    }

    private bool TryProtectLocalFolders(RemoteToLocalMapping mapping)
    {
        var cloudFilesFolderPath = mapping.Local.Path
                                   ?? throw new InvalidOperationException("Cloud files folder path is not specified");

        var accountRootFolderPath = Path.GetDirectoryName(cloudFilesFolderPath)
                                    ?? throw new InvalidOperationException("Account root folder path cannot be obtained");

        return _syncFolderProtector.ProtectFolder(accountRootFolderPath, FolderProtectionType.Ancestor) &&
               _syncFolderProtector.ProtectFolder(cloudFilesFolderPath, FolderProtectionType.Leaf);
    }

    private Task<MappingErrorCode?> TryAddShellFolderAsync(RemoteToLocalMapping mapping)
    {
        switch (mapping.SyncMethod)
        {
            case SyncMethod.Classic:
                AddClassicSyncShellFolder(mapping);
                return Task.FromResult<MappingErrorCode?>(null);

            case SyncMethod.OnDemand:
                return TryAddOnDemandSyncRootAsync(mapping);

            default:
                throw new InvalidEnumArgumentException(nameof(mapping.SyncMethod), (int)mapping.SyncMethod, typeof(SyncMethod));
        }
    }

    private void AddClassicSyncShellFolder(RemoteToLocalMapping mapping)
    {
        var path = Path.GetDirectoryName(mapping.Local.Path);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        _shellSyncFolderRegistry.Register(path);
    }

    private async Task<MappingErrorCode?> TryAddOnDemandSyncRootAsync(RemoteToLocalMapping mapping)
    {
        var errorInfo = await _onDemandSyncRootRegistry.TryAddOnDemandSyncRootAsync(mapping).ConfigureAwait(false);
        return errorInfo?.ErrorCode;
    }

    private void CustomizeUserDataFolderAppearance()
    {
        if (_isUserDataFolderAppearanceCustomized)
        {
            return;
        }

        const string infoTip = "Contains files synchronized with Proton Drive.";
        _isUserDataFolderAppearanceCustomized =
            _folderAppearanceCustomizer.TrySetIconAndInfoTip(_appConfig.UserDataPath, _appConfig.AppLaunchPath, infoTip);
    }
}
