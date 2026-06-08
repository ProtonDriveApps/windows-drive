using ProtonDrive.App.Settings;
using ProtonDrive.Sync.Shared.FileSystem.Integration;
using ProtonDrive.Sync.Shared.FileSystem.OnDemand;
using ProtonDrive.Sync.Shared.Shell;

namespace ProtonDrive.App.Mapping.Teardown;

internal sealed class ForeignDeviceMappingTeardownStep
{
    private readonly ILocalFolderService _localFolderService;
    private readonly ILocalSpecialSubfoldersDeletionStep _specialFoldersDeletion;
    private readonly IOnDemandSyncRootRegistry _onDemandSyncRootRegistry;
    private readonly ISyncFolderStructureProtector _syncFolderProtector;
    private readonly IPlaceholderToRegularItemConverter _placeholderConverter;

    public ForeignDeviceMappingTeardownStep(
        ILocalFolderService localFolderService,
        ILocalSpecialSubfoldersDeletionStep specialFoldersDeletion,
        IOnDemandSyncRootRegistry onDemandSyncRootRegistry,
        ISyncFolderStructureProtector syncFolderProtector,
        IPlaceholderToRegularItemConverter placeholderConverter)
    {
        _localFolderService = localFolderService;
        _specialFoldersDeletion = specialFoldersDeletion;
        _onDemandSyncRootRegistry = onDemandSyncRootRegistry;
        _syncFolderProtector = syncFolderProtector;
        _placeholderConverter = placeholderConverter;
    }

    public async Task<MappingErrorCode> TearDownAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.ForeignDevice)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        cancellationToken.ThrowIfCancellationRequested();

        TryUnprotectLocalFolder(mapping);

        if (!TryConvertToRegularFolder(mapping))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        if (!TryDeleteSpecialSubfolders(mapping))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        if (!await TryRemoveOnDemandSyncRootAsync(mapping).ConfigureAwait(false))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        TryDeleteFolderIfEmpty(mapping);

        return MappingErrorCode.None;
    }

    private bool TryUnprotectLocalFolder(RemoteToLocalMapping mapping)
    {
        var foreignDeviceFolderPath = mapping.Local.Path;

        // The foreign devices folder ("Other computers") is not unprotected as part of tearing down the foreign device mapping.
        // It is unprotected when tearing down the cloud files mapping.
        return _syncFolderProtector.UnprotectFolder(foreignDeviceFolderPath, FolderProtectionType.Leaf);
    }

    private bool TryConvertToRegularFolder(RemoteToLocalMapping mapping)
    {
        return _placeholderConverter.TryConvertToRegularFolder(mapping.Local.Path, skipRoot: true);
    }

    private bool TryDeleteSpecialSubfolders(RemoteToLocalMapping mapping)
    {
        _specialFoldersDeletion.DeleteSpecialSubfolders(mapping.Local.Path);

        return true;
    }

    private async Task<bool> TryRemoveOnDemandSyncRootAsync(RemoteToLocalMapping mapping)
    {
        if (mapping.SyncMethod is not SyncMethod.OnDemand)
        {
            return true;
        }

        var root = new OnDemandSyncRootInfo(
            Path: mapping.Local.Path,
            RootId: mapping.Id.ToString(),
            Visibility: ShellFolderVisibility.Hidden,
            SiblingsGrouping: ShellFolderSiblingsGrouping.Grouped);

        return await _onDemandSyncRootRegistry.TryUnregisterAsync(root).ConfigureAwait(false);
    }

    private void TryDeleteFolderIfEmpty(RemoteToLocalMapping mapping)
    {
        _localFolderService.TryDeleteEmptyFolder(mapping.Local.Path);
    }
}
