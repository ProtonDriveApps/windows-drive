using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.OnDemand;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.SharedWithMe.SharedWithMeRootFolder;

internal sealed class SharedWithMeRootFolderMappingSetupFinalizationStep
{
    private readonly ISyncFolderStructureProtector _syncFolderProtector;
    private readonly IOnDemandSyncRootRegistry _onDemandSyncRootRegistry;

    public SharedWithMeRootFolderMappingSetupFinalizationStep(
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

        if (await TryAddOnDemandSyncRootAsync(mapping).ConfigureAwait(false) is { } errorCode)
        {
            return errorCode;
        }

        return MappingErrorCode.None;
    }

    private bool TryProtectLocalFolder(RemoteToLocalMapping mapping)
    {
        var sharedWithMeRootFolderPath = mapping.Local.Path
            ?? throw new InvalidOperationException("Shared with me root folder path is not specified");

        return _syncFolderProtector.ProtectFolder(sharedWithMeRootFolderPath, FolderProtectionType.AncestorWithFiles);
    }

    private async Task<MappingErrorCode?> TryAddOnDemandSyncRootAsync(RemoteToLocalMapping mapping)
    {
        var errorInfo = await _onDemandSyncRootRegistry.TryAddOnDemandSyncRootAsync(mapping).ConfigureAwait(false);
        return errorInfo?.ErrorCode;
    }
}
