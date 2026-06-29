using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup.SharedWithMe.SharedWithMeItem;

internal sealed class SharedWithMeItemMappingSetupFinalizationStep
{
    private readonly ISyncFolderStructureProtector _syncFolderProtector;

    public SharedWithMeItemMappingSetupFinalizationStep(ISyncFolderStructureProtector syncFolderProtector)
    {
        _syncFolderProtector = syncFolderProtector;
    }

    public Task<MappingErrorCode> FinishSetupAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.SharedWithMeItem)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        if (mapping.Remote.RootItemType is LinkType.Folder)
        {
            TryProtectSharedWithMeFolderItem(mapping.Local.Path);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(MappingErrorCode.None);
    }

    private bool TryProtectSharedWithMeFolderItem(string folderPath)
    {
        var sharedWithMeRootFolderPath = Path.GetDirectoryName(folderPath)
            ?? throw new InvalidOperationException("Shared with me root folder path cannot be obtained");

        return _syncFolderProtector.ProtectFolder(sharedWithMeRootFolderPath, FolderProtectionType.AncestorWithFiles)
            && _syncFolderProtector.ProtectFolder(folderPath, FolderProtectionType.Leaf);
    }
}
