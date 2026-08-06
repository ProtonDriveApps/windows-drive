using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.OnDemand;
using Proton.Drive.Sdk.Sync.Shared.Shell;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup;

internal static class OnDemandSyncRootRegistryExtensions
{
    public static async Task<MappingErrorInfo?> TryAddOnDemandSyncRootAsync(this IOnDemandSyncRootRegistry syncRootRegistry, RemoteToLocalMapping mapping)
    {
        if (mapping.SyncMethod is not SyncMethod.OnDemand && !mapping.IsStorageOptimizationPending())
        {
            return null;
        }

        var root = new OnDemandSyncRootInfo(
            Path: mapping.Local.Path,
            RootId: mapping.Id.ToString(),
            Visibility: GetVisibility(mapping),
            SiblingsGrouping: GetSiblingsGrouping(mapping));

        var (verdict, conflictingProviderName) = await syncRootRegistry.VerifyAsync(root).ConfigureAwait(false);

        // If the mapping status is Complete, then the on-demand sync root should already be registered.
        // Registering the same sync root on top of existing registration works, it updates the characteristics of the root, if any have changed.
        // We care about the unexpected un-registration of previously registered on-demand sync root, because if we register it again,
        // the folder might have placeholder files removed, so the app would delete them on Proton Drive, which is not what the user expects.
        if (mapping.Status is MappingStatus.Complete && mapping.SyncMethod is SyncMethod.OnDemand)
        {
            switch (verdict)
            {
                case OnDemandSyncRootVerificationVerdict.Valid:
                    return null;
                case OnDemandSyncRootVerificationVerdict.VerificationFailed:
                    return new MappingErrorInfo(MappingErrorCode.LocalFileSystemAccessFailed);
                case OnDemandSyncRootVerificationVerdict.NotRegistered:
                    return new MappingErrorInfo(MappingErrorCode.OnDemandSyncRootNotRegistered);
                case OnDemandSyncRootVerificationVerdict.MissingSyncRootFlag:
                    return new MappingErrorInfo(MappingErrorCode.OnDemandSyncRootMissingSyncRootFlag);
            }
        }

        switch (verdict)
        {
            case OnDemandSyncRootVerificationVerdict.ConflictingRootExists:
                return new MappingErrorInfo(MappingErrorCode.ConflictingOnDemandSyncRootExists, conflictingProviderName);
            case OnDemandSyncRootVerificationVerdict.ConflictingDescendantRootExists:
                return new MappingErrorInfo(MappingErrorCode.ConflictingDescendantOnDemandSyncRootExists, conflictingProviderName);
        }

        return (await syncRootRegistry.TryRegisterAsync(root).ConfigureAwait(false))
            ? null
            : new MappingErrorInfo(MappingErrorCode.LocalFileSystemAccessFailed);
    }

    private static ShellFolderVisibility GetVisibility(RemoteToLocalMapping mapping)
    {
        // Cloud files and Shared with me root folder are grouped together and shown in the
        // Windows shell as a single "Proton drive" folder.
        return mapping.Type switch
        {
            MappingType.CloudFiles => ShellFolderVisibility.Visible,
            MappingType.SharedWithMeRootFolder => ShellFolderVisibility.Visible,
            _ => ShellFolderVisibility.Hidden,
        };
    }

    private static ShellFolderSiblingsGrouping GetSiblingsGrouping(RemoteToLocalMapping mapping)
    {
        return mapping.Type switch
        {
            MappingType.HostDeviceFolder => ShellFolderSiblingsGrouping.Independent,
            _ => ShellFolderSiblingsGrouping.Grouped,
        };
    }
}
