using System.ComponentModel;
using ProtonDrive.App.Settings;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Sync.Shared.FileSystem.Integration;
using ProtonDrive.Sync.Shared.FileSystem.OnDemand;

namespace ProtonDrive.App.Mapping.Teardown;

internal sealed class SharedWithMeItemMappingTeardownStep
{
    private readonly ILocalSpecialSubfoldersDeletionStep _specialFoldersDeletion;
    private readonly IReadOnlyFileAttributeRemover _readOnlyFileAttributeRemover;
    private readonly IPlaceholderToRegularItemConverter _placeholderConverter;
    private readonly ISyncFolderStructureProtector _syncFolderProtector;

    public SharedWithMeItemMappingTeardownStep(
        ILocalSpecialSubfoldersDeletionStep specialFoldersDeletion,
        IReadOnlyFileAttributeRemover readOnlyFileAttributeRemover,
        IPlaceholderToRegularItemConverter placeholderConverter,
        ISyncFolderStructureProtector syncFolderProtector)
    {
        _specialFoldersDeletion = specialFoldersDeletion;
        _readOnlyFileAttributeRemover = readOnlyFileAttributeRemover;
        _placeholderConverter = placeholderConverter;
        _syncFolderProtector = syncFolderProtector;
    }

    public Task<MappingErrorCode> TearDownAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Type is not MappingType.SharedWithMeItem)
        {
            throw new ArgumentException("Mapping type has unexpected value", nameof(mapping));
        }

        return mapping.Remote.RootItemType switch
        {
            LinkType.Folder => Task.FromResult(TearDownFolder(mapping, cancellationToken)),
            LinkType.File => Task.FromResult(TearDownFile(mapping)),
            _ => throw new InvalidEnumArgumentException(nameof(mapping.Remote.RootItemType), (int)mapping.Remote.RootItemType, typeof(LinkType)),
        };
    }

    private MappingErrorCode TearDownFile(RemoteToLocalMapping mapping)
    {
        if (mapping.Remote.IsReadOnly)
        {
            if (!_syncFolderProtector.UnprotectFile(mapping.Local.Path, FileProtectionType.ReadOnly))
            {
                return MappingErrorCode.LocalFileSystemAccessFailed;
            }

            if (!_readOnlyFileAttributeRemover.TryRemoveFileReadOnlyAttribute(mapping.Local.Path))
            {
                return MappingErrorCode.LocalFileSystemAccessFailed;
            }
        }

        if (!_placeholderConverter.TryConvertToRegularFile(mapping.Local.Path))
        {
            return MappingErrorCode.LocalFileSystemAccessFailed;
        }

        return MappingErrorCode.None;
    }

    private MappingErrorCode TearDownFolder(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (mapping.Remote.IsReadOnly)
        {
            if (!_syncFolderProtector.UnprotectBranch(mapping.Local.Path, FolderProtectionType.ReadOnly, FileProtectionType.ReadOnly))
            {
                return MappingErrorCode.LocalFileSystemAccessFailed;
            }

            if (!_readOnlyFileAttributeRemover.TryRemoveFileReadOnlyAttributeInFolder(mapping.Local.Path))
            {
                return MappingErrorCode.LocalFileSystemAccessFailed;
            }
        }

        TryUnprotectLocalFolder(mapping);

        try
        {
            if (!TryConvertToRegularFolder(mapping))
            {
                return MappingErrorCode.LocalFileSystemAccessFailed;
            }

            if (!TryDeleteSpecialSubfolders(mapping))
            {
                return MappingErrorCode.LocalFileSystemAccessFailed;
            }

            return MappingErrorCode.None;
        }
        finally
        {
            TryProtectSharedWithMeRootFolder(mapping);
        }
    }

    private void TryUnprotectLocalFolder(RemoteToLocalMapping mapping)
    {
        var folderPath = mapping.Local.Path
            ?? throw new InvalidOperationException("Shared with me item path is not specified");

        var sharedWithMeRootFolderPath = Path.GetDirectoryName(folderPath)
            ?? throw new InvalidOperationException("Shared with me root folder path cannot be obtained");

        _syncFolderProtector.UnprotectFolder(sharedWithMeRootFolderPath, FolderProtectionType.AncestorWithFiles);
        _syncFolderProtector.UnprotectFolder(folderPath, FolderProtectionType.Leaf);
    }

    private void TryProtectSharedWithMeRootFolder(RemoteToLocalMapping mapping)
    {
        var folderPath = mapping.Local.Path
            ?? throw new InvalidOperationException("Shared with me item path is not specified");

        var sharedWithMeRootFolderPath = Path.GetDirectoryName(folderPath)
            ?? throw new InvalidOperationException("Shared with me root folder path cannot be obtained");

        // Folder might not exist, if mapping was deleted before creating local folder or if the user deleted the folder.
        // We ignore failure to protect parent folder.
        _syncFolderProtector.ProtectFolder(sharedWithMeRootFolderPath, FolderProtectionType.AncestorWithFiles);
    }

    private bool TryDeleteSpecialSubfolders(RemoteToLocalMapping mapping)
    {
        _specialFoldersDeletion.DeleteSpecialSubfolders(mapping.Local.Path);

        return true;
    }

    private bool TryConvertToRegularFolder(RemoteToLocalMapping mapping)
    {
        return _placeholderConverter.TryConvertToRegularFolder(mapping.Local.Path, skipRoot: false);
    }
}
