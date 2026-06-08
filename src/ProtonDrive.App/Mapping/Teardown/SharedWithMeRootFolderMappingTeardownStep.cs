using System.ComponentModel;
using ProtonDrive.App.Settings;
using ProtonDrive.Sync.Shared.FileSystem.Integration;
using ProtonDrive.Sync.Shared.FileSystem.OnDemand;
using ProtonDrive.Sync.Shared.Shell;

namespace ProtonDrive.App.Mapping.Teardown;

internal sealed class SharedWithMeRootFolderMappingTeardownStep
{
    private readonly ILocalFolderService _localFolderService;
    private readonly ISyncFolderStructureProtector _syncFolderProtector;
    private readonly IOnDemandSyncRootRegistry _onDemandSyncRootRegistry;

    public SharedWithMeRootFolderMappingTeardownStep(
        ILocalFolderService localFolderService,
        ISyncFolderStructureProtector syncFolderProtector,
        IOnDemandSyncRootRegistry onDemandSyncRootRegistry)
    {
        _localFolderService = localFolderService;
        _syncFolderProtector = syncFolderProtector;
        _onDemandSyncRootRegistry = onDemandSyncRootRegistry;
    }

    public async Task<MappingErrorCode> TearDownAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.SharedWithMeRootFolder)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        var sharedWithMeRootFolderPath = mapping.Local.Path
            ?? throw new InvalidOperationException("Shared with me root folder path is not specified");

        var accountRootFolderPath = Path.GetDirectoryName(sharedWithMeRootFolderPath)
            ?? throw new InvalidOperationException("Account root folder path cannot be obtained");

        TryUnprotectLocalFolders();

        if (!await TryRemoveShellFolderAsync(mapping).ConfigureAwait(false))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        TryDeleteFolderIfEmpty(sharedWithMeRootFolderPath);

        _syncFolderProtector.ProtectFolder(accountRootFolderPath, FolderProtectionType.Ancestor);

        return MappingErrorCode.None;

        bool TryUnprotectLocalFolders()
        {
            return _syncFolderProtector.UnprotectFolder(accountRootFolderPath, FolderProtectionType.Ancestor) &&
                _syncFolderProtector.UnprotectFolder(sharedWithMeRootFolderPath, FolderProtectionType.AncestorWithFiles);
        }
    }

    private void TryDeleteFolderIfEmpty(string folderPath)
    {
        _localFolderService.TryDeleteEmptyFolder(folderPath);
    }

    private Task<bool> TryRemoveShellFolderAsync(RemoteToLocalMapping mapping)
    {
        if (mapping.SyncMethod is not SyncMethod.OnDemand)
        {
            throw new InvalidEnumArgumentException(nameof(mapping.SyncMethod), (int)mapping.SyncMethod, typeof(SyncMethod));
        }

        var root = new OnDemandSyncRootInfo(
            Path: mapping.Local.Path,
            RootId: mapping.Id.ToString(),
            Visibility: ShellFolderVisibility.Visible,
            SiblingsGrouping: ShellFolderSiblingsGrouping.Grouped);

        return _onDemandSyncRootRegistry.TryUnregisterAsync(root);
    }
}
