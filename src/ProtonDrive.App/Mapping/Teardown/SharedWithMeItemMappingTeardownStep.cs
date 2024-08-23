using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.App.Mapping.Teardown;

internal sealed class SharedWithMeItemMappingTeardownStep
{
    private readonly IPlaceholderToRegularItemConverter _classicFileSystemConverter;
    private readonly ISyncFolderStructureProtector _syncFolderProtector;

    public SharedWithMeItemMappingTeardownStep(
        IPlaceholderToRegularItemConverter classicFileSystemConverter,
        ISyncFolderStructureProtector syncFolderProtector)
    {
        _classicFileSystemConverter = classicFileSystemConverter;
        _syncFolderProtector = syncFolderProtector;
    }

    public Task<MappingErrorCode> TearDownAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.SharedWithMeItem)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        return mapping.Remote.RootLinkType switch
        {
            LinkType.Folder => TearDownFolderAsync(mapping, cancellationToken),
            LinkType.File => TearDownFileAsync(mapping),
            _ => throw new InvalidEnumArgumentException(nameof(mapping.Remote.RootLinkType), (int)mapping.Remote.RootLinkType, typeof(LinkType)),
        };
    }

    private Task<MappingErrorCode> TearDownFileAsync(RemoteToLocalMapping mapping)
    {
        if (mapping.Remote.RootFolderName is null)
        {
            return Task.FromResult(MappingErrorCode.LocalFileSystemAccessFailed);
        }

        var itemPath = Path.Combine(mapping.Local.RootFolderPath, mapping.Remote.RootFolderName);

        return Task.FromResult(!_classicFileSystemConverter.TryConvertToRegularFile(itemPath)
            ? MappingErrorCode.LocalFileSystemAccessFailed
            : MappingErrorCode.None);
    }

    private Task<MappingErrorCode> TearDownFolderAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TryUnprotectLocalFolder(mapping);

        try
        {
            return Task.FromResult(
                !_classicFileSystemConverter.TryConvertToRegularFolder(mapping.Local.RootFolderPath)
                    ? MappingErrorCode.LocalFileSystemAccessFailed
                    : MappingErrorCode.None);
        }
        finally
        {
            TryProtectSharedWithMeItemsFolder(mapping);
        }
    }

    private void TryUnprotectLocalFolder(RemoteToLocalMapping mapping)
    {
        var folderPath = mapping.Local.RootFolderPath
            ?? throw new InvalidOperationException("Shared with me item path is not specified");

        var sharedWithMeItemsFolderPath = Path.GetDirectoryName(folderPath)
            ?? throw new InvalidOperationException("Shared with me items folder path cannot be obtained");

        _syncFolderProtector.Unprotect(sharedWithMeItemsFolderPath, FolderProtectionType.AncestorWithFiles);
        _syncFolderProtector.Unprotect(folderPath, FolderProtectionType.Leaf);
    }

    private void TryProtectSharedWithMeItemsFolder(RemoteToLocalMapping mapping)
    {
        var folderPath = mapping.Local.RootFolderPath
            ?? throw new InvalidOperationException("Shared with me item path is not specified");

        var sharedWithMeItemsFolderPath = Path.GetDirectoryName(folderPath)
            ?? throw new InvalidOperationException("Shared with me items folder path cannot be obtained");

        // Folder might not exist, if mapping was deleted before creating local folder or if the user deleted the folder.
        // We ignore failure to protect parent folder.
        _syncFolderProtector.Protect(sharedWithMeItemsFolderPath, FolderProtectionType.AncestorWithFiles);
    }
}
