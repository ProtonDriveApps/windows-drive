using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;

namespace ProtonDrive.App.Mapping.Setup.ForeignDevices;

internal sealed class ForeignDeviceMappingSetupFinalizationStep
{
    private readonly ISyncFolderStructureProtector _syncFolderProtector;
    private readonly IOnDemandSyncRootRegistry _onDemandSyncRootRegistry;

    public ForeignDeviceMappingSetupFinalizationStep(
        ISyncFolderStructureProtector syncFolderProtector,
        IOnDemandSyncRootRegistry onDemandSyncRootRegistry)
    {
        _syncFolderProtector = syncFolderProtector;
        _onDemandSyncRootRegistry = onDemandSyncRootRegistry;
    }

    public async Task<MappingErrorCode> FinishSetupAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.ForeignDevice)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        TryProtectLocalFolders(mapping);

        cancellationToken.ThrowIfCancellationRequested();

        if (!await TryAddOnDemandSyncRootAsync(mapping).ConfigureAwait(false))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return MappingErrorCode.None;
    }

    private bool TryProtectLocalFolders(RemoteToLocalMapping mapping)
    {
        var foreignDeviceFolderPath = mapping.Local.RootFolderPath
                                      ?? throw new InvalidOperationException("Foreign device folder path is not specified");

        var foreignDevicesFolderPath = Path.GetDirectoryName(foreignDeviceFolderPath)
                                       ?? throw new InvalidOperationException("Foreign devices folder path cannot be obtained");

        return _syncFolderProtector.Protect(foreignDevicesFolderPath, FolderProtectionType.Ancestor) &&
               _syncFolderProtector.Protect(foreignDeviceFolderPath, FolderProtectionType.Leaf);
    }

    private async Task<bool> TryAddOnDemandSyncRootAsync(RemoteToLocalMapping mapping)
    {
        if (mapping.SyncMethod is not SyncMethod.OnDemand)
        {
            return true;
        }

        var root = new OnDemandSyncRootInfo(Path: mapping.Local.RootFolderPath, RootId: mapping.Id.ToString(), ShellFolderVisibility.Hidden);

        return await _onDemandSyncRootRegistry.TryRegisterAsync(root).ConfigureAwait(false);
    }
}
