using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Shared.Configuration;

namespace ProtonDrive.App.Mapping.Setup.CloudFiles;

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

        if (!await TryAddShellFolderAsync(mapping).ConfigureAwait(false))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        cancellationToken.ThrowIfCancellationRequested();

        CustomizeUserDataFolderAppearance();

        return MappingErrorCode.None;
    }

    private bool TryProtectLocalFolders(RemoteToLocalMapping mapping)
    {
        var cloudFilesFolderPath = mapping.Local.RootFolderPath
                                   ?? throw new InvalidOperationException("Cloud files folder path is not specified");

        var accountRootFolderPath = Path.GetDirectoryName(cloudFilesFolderPath)
                                    ?? throw new InvalidOperationException("Account root folder path cannot be obtained");

        return _syncFolderProtector.Protect(accountRootFolderPath, FolderProtectionType.Ancestor) &&
               _syncFolderProtector.Protect(cloudFilesFolderPath, FolderProtectionType.Leaf);
    }

    private Task<bool> TryAddShellFolderAsync(RemoteToLocalMapping mapping)
    {
        switch (mapping.SyncMethod)
        {
            case SyncMethod.Classic:
                AddClassicSyncShellFolder(mapping);
                return Task.FromResult(true);

            case SyncMethod.OnDemand:
                return TryAddOnDemandSyncRootAsync(mapping);

            default:
                throw new InvalidEnumArgumentException(nameof(mapping.SyncMethod), (int)mapping.SyncMethod, typeof(SyncMethod));
        }
    }

    private void AddClassicSyncShellFolder(RemoteToLocalMapping mapping)
    {
        var path = Path.GetDirectoryName(mapping.Local.RootFolderPath);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        _shellSyncFolderRegistry.Register(path);
    }

    private Task<bool> TryAddOnDemandSyncRootAsync(RemoteToLocalMapping mapping)
    {
        var root = new OnDemandSyncRootInfo(Path: mapping.Local.RootFolderPath, RootId: mapping.Id.ToString(), ShellFolderVisibility.Visible);

        return _onDemandSyncRootRegistry.TryRegisterAsync(root);
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
