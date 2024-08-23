using System;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;

namespace ProtonDrive.App.Mapping.Setup.SharedWithMe.SharedWithMeItemsFolder;

internal sealed class SharedWithMeItemsFolderMappingSetupFinalizationStep
{
    private readonly ISyncFolderStructureProtector _syncFolderProtector;
    private readonly IOnDemandSyncRootRegistry _onDemandSyncRootRegistry;

    public SharedWithMeItemsFolderMappingSetupFinalizationStep(
        ISyncFolderStructureProtector syncFolderProtector,
        IOnDemandSyncRootRegistry onDemandSyncRootRegistry)
    {
        _syncFolderProtector = syncFolderProtector;
        _onDemandSyncRootRegistry = onDemandSyncRootRegistry;
    }

    public async Task<MappingErrorCode> FinishSetupAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.SharedWithMeRootFolder)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        TryProtectLocalFolder(mapping);

        cancellationToken.ThrowIfCancellationRequested();

        if (!await TryAddOnDemandSyncRootAsync(mapping).ConfigureAwait(false))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return MappingErrorCode.None;
    }

    private bool TryProtectLocalFolder(RemoteToLocalMapping mapping)
    {
        var sharedWithMeItemsFolderPath = mapping.Local.RootFolderPath
            ?? throw new InvalidOperationException("Shared with me items folder path is not specified");

        return _syncFolderProtector.Protect(sharedWithMeItemsFolderPath, FolderProtectionType.AncestorWithFiles);
    }

    private Task<bool> TryAddOnDemandSyncRootAsync(RemoteToLocalMapping mapping)
    {
        if (mapping.SyncMethod is not SyncMethod.OnDemand)
        {
            return Task.FromResult(true);
        }

        var root = new OnDemandSyncRootInfo(Path: mapping.Local.RootFolderPath, RootId: mapping.Id.ToString(), ShellFolderVisibility.Visible);

        return _onDemandSyncRootRegistry.TryRegisterAsync(root);
    }
}
