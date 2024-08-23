using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Shared.Configuration;

namespace ProtonDrive.App.Mapping.Teardown;

internal sealed class CloudFilesMappingTeardownStep
{
    private readonly AppConfig _appConfig;
    private readonly ILocalSpecialSubfoldersDeletionStep _specialFoldersDeletion;
    private readonly ISyncFolderStructureProtector _syncFolderProtector;
    private readonly IShellSyncFolderRegistry _shellSyncFolderRegistry;
    private readonly IOnDemandSyncRootRegistry _onDemandSyncRootRegistry;

    public CloudFilesMappingTeardownStep(
        AppConfig appConfig,
        ILocalSpecialSubfoldersDeletionStep specialFoldersDeletion,
        ISyncFolderStructureProtector syncFolderProtector,
        IShellSyncFolderRegistry shellSyncFolderRegistry,
        IOnDemandSyncRootRegistry onDemandSyncRootRegistry)
    {
        _appConfig = appConfig;
        _specialFoldersDeletion = specialFoldersDeletion;
        _syncFolderProtector = syncFolderProtector;
        _shellSyncFolderRegistry = shellSyncFolderRegistry;
        _onDemandSyncRootRegistry = onDemandSyncRootRegistry;
    }

    public async Task<MappingErrorCode> TearDownAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.CloudFiles)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        cancellationToken.ThrowIfCancellationRequested();

        TryUnprotectLocalFolders(mapping);

        if (TryDeleteSpecialSubfolders(mapping) &&
            await TryRemoveShellFolderAsync(mapping).ConfigureAwait(false))
        {
            return MappingErrorCode.None;
        }

        return MappingErrorCode.LocalFileSystemAccessFailed;
    }

    private bool TryUnprotectLocalFolders(RemoteToLocalMapping mapping)
    {
        var cloudFilesFolderPath = mapping.Local.RootFolderPath;
        if (string.IsNullOrEmpty(cloudFilesFolderPath))
        {
            return true;
        }

        if (!mapping.TryGetAccountRootFolderPath(out var accountRootFolderPath))
        {
            throw new InvalidOperationException($"Unable to obtain account root folder path from mapping with Id={mapping.Id}");
        }

        // The foreign devices folder ("Other computers") is not unprotected as part of tearing down the foreign devices mappings,
        // therefore, it is unprotected here.
        var foreignDevicesFolderPath = Path.Combine(accountRootFolderPath, _appConfig.FolderNames.ForeignDevicesFolderName);

        return _syncFolderProtector.Unprotect(cloudFilesFolderPath, FolderProtectionType.Leaf) &&
               _syncFolderProtector.Unprotect(accountRootFolderPath, FolderProtectionType.Ancestor) &&
               _syncFolderProtector.Unprotect(foreignDevicesFolderPath, FolderProtectionType.Ancestor);
    }

    private bool TryDeleteSpecialSubfolders(RemoteToLocalMapping mapping)
    {
        _specialFoldersDeletion.DeleteSpecialSubfolders(mapping.Local.RootFolderPath);

        return true;
    }

    private Task<bool> TryRemoveShellFolderAsync(RemoteToLocalMapping mapping)
    {
        switch (mapping.SyncMethod)
        {
            case SyncMethod.Classic:
                RemoveClassicSyncShellFolder();
                return Task.FromResult(true);

            case SyncMethod.OnDemand:
                return TryRemoveOnDemandSyncRootAsync(mapping);

            default:
                throw new InvalidEnumArgumentException(nameof(mapping.SyncMethod), (int)mapping.SyncMethod, typeof(SyncMethod));
        }
    }

    private void RemoveClassicSyncShellFolder()
    {
        _shellSyncFolderRegistry.Unregister();
    }

    private Task<bool> TryRemoveOnDemandSyncRootAsync(RemoteToLocalMapping mapping)
    {
        var root = new OnDemandSyncRootInfo(Path: mapping.Local.RootFolderPath, RootId: mapping.Id.ToString(), ShellFolderVisibility.Visible);

        return _onDemandSyncRootRegistry.TryUnregisterAsync(root);
    }
}
