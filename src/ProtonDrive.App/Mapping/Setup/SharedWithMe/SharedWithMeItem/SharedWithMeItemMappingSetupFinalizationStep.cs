using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.App.Mapping.Setup.SharedWithMe.SharedWithMeItem;

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

        if (mapping.Remote.RootLinkType is LinkType.Folder)
        {
            TryProtectSharedWithMeFolderItem(mapping.Local.RootFolderPath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(MappingErrorCode.None);
    }

    private bool TryProtectSharedWithMeFolderItem(string folderPath)
    {
        var sharedWithMeItemsFolderPath = Path.GetDirectoryName(folderPath)
                                       ?? throw new InvalidOperationException("Shared with me items folder path cannot be obtained");

        return _syncFolderProtector.Protect(sharedWithMeItemsFolderPath, FolderProtectionType.AncestorWithFiles) &&
               _syncFolderProtector.Protect(folderPath, FolderProtectionType.Leaf);
    }
}
