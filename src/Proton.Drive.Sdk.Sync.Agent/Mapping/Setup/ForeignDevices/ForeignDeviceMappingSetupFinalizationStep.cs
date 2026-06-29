using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.OnDemand;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.ForeignDevices;

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

        if (await TryAddOnDemandSyncRootAsync(mapping).ConfigureAwait(false) is { } errorCode)
        {
            return errorCode;
        }

        return MappingErrorCode.None;
    }

    private bool TryProtectLocalFolders(RemoteToLocalMapping mapping)
    {
        var foreignDeviceFolderPath = mapping.Local.Path
                                      ?? throw new InvalidOperationException("Foreign device folder path is not specified");

        var foreignDevicesFolderPath = Path.GetDirectoryName(foreignDeviceFolderPath)
                                       ?? throw new InvalidOperationException("Foreign devices folder path cannot be obtained");

        return _syncFolderProtector.ProtectFolder(foreignDevicesFolderPath, FolderProtectionType.Ancestor) &&
               _syncFolderProtector.ProtectFolder(foreignDeviceFolderPath, FolderProtectionType.Leaf);
    }

    private async Task<MappingErrorCode?> TryAddOnDemandSyncRootAsync(RemoteToLocalMapping mapping)
    {
        var errorInfo = await _onDemandSyncRootRegistry.TryAddOnDemandSyncRootAsync(mapping).ConfigureAwait(false);
        return errorInfo?.ErrorCode;
    }
}
