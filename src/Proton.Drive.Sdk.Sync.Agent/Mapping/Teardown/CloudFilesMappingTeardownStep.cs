using System.ComponentModel;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.OnDemand;
using Proton.Drive.Sdk.Sync.Shared.Shell;
using Proton.Drive.Shared.Configuration;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Teardown;

internal sealed class CloudFilesMappingTeardownStep
{
    private readonly AppConfig _appConfig;
    private readonly ILocalFolderService _localFolderService;
    private readonly ILocalSpecialSubfoldersDeletionStep _specialFoldersDeletion;
    private readonly ISyncFolderStructureProtector _syncFolderProtector;
    private readonly IPlaceholderToRegularItemConverter _placeholderConverter;
    private readonly IShellSyncFolderRegistry _shellSyncFolderRegistry;
    private readonly IOnDemandSyncRootRegistry _onDemandSyncRootRegistry;

    public CloudFilesMappingTeardownStep(
        AppConfig appConfig,
        ILocalFolderService localFolderService,
        ILocalSpecialSubfoldersDeletionStep specialFoldersDeletion,
        ISyncFolderStructureProtector syncFolderProtector,
        IPlaceholderToRegularItemConverter placeholderConverter,
        IShellSyncFolderRegistry shellSyncFolderRegistry,
        IOnDemandSyncRootRegistry onDemandSyncRootRegistry)
    {
        _appConfig = appConfig;
        _localFolderService = localFolderService;
        _specialFoldersDeletion = specialFoldersDeletion;
        _syncFolderProtector = syncFolderProtector;
        _placeholderConverter = placeholderConverter;
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

        if (!TryConvertToRegularFolder(mapping))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        if (!TryDeleteSpecialSubfolders(mapping))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        if (!await TryRemoveShellFolderAsync(mapping).ConfigureAwait(false))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        TryDeleteFolderIfEmpty(mapping);
        TryDeleteForeignDevicesFolderIfEmpty(mapping);

        return MappingErrorCode.None;
    }

    private bool TryUnprotectLocalFolders(RemoteToLocalMapping mapping)
    {
        var cloudFilesFolderPath = mapping.Local.Path;
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

        return _syncFolderProtector.UnprotectFolder(cloudFilesFolderPath, FolderProtectionType.Leaf) &&
               _syncFolderProtector.UnprotectFolder(accountRootFolderPath, FolderProtectionType.Ancestor) &&
               _syncFolderProtector.UnprotectFolder(foreignDevicesFolderPath, FolderProtectionType.Ancestor);
    }

    private bool TryConvertToRegularFolder(RemoteToLocalMapping mapping)
    {
        if (mapping.SyncMethod is not SyncMethod.OnDemand)
        {
            return true;
        }

        // The folder is on-demand sync root. Deleting the folder or converting it to regular one would automatically unregister
        // on-demand sync root, so that subsequent un-registration attempt fails. Also, it might make the folder inaccessible.
        return _placeholderConverter.TryConvertToRegularFolder(mapping.Local.Path, skipRoot: true);
    }

    private bool TryDeleteSpecialSubfolders(RemoteToLocalMapping mapping)
    {
        _specialFoldersDeletion.DeleteSpecialSubfolders(mapping.Local.Path);

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
        var root = new OnDemandSyncRootInfo(
            Path: mapping.Local.Path,
            RootId: mapping.Id.ToString(),
            Visibility: ShellFolderVisibility.Visible,
            SiblingsGrouping: ShellFolderSiblingsGrouping.Grouped);

        return _onDemandSyncRootRegistry.TryUnregisterAsync(root);
    }

    private void TryDeleteFolderIfEmpty(RemoteToLocalMapping mapping)
    {
        _localFolderService.TryDeleteEmptyFolder(mapping.Local.Path);
    }

    private void TryDeleteForeignDevicesFolderIfEmpty(RemoteToLocalMapping cloudFilesMapping)
    {
        if (!cloudFilesMapping.TryGetAccountRootFolderPath(out var accountRootFolderPath))
        {
            throw new InvalidOperationException($"Unable to obtain account root folder path from mapping with Id={cloudFilesMapping.Id}");
        }

        // The foreign devices folder ("Other computers") is not deleted as part of tearing down the foreign device mappings,
        // therefore, it is deleted here, if empty.
        var foreignDevicesFolderPath = Path.Combine(accountRootFolderPath, _appConfig.FolderNames.ForeignDevicesFolderName);

        _localFolderService.TryDeleteEmptyFolder(foreignDevicesFolderPath);
    }
}
