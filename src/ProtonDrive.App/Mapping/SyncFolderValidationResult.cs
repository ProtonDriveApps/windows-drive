namespace ProtonDrive.App.Mapping;

public enum SyncFolderValidationResult
{
    Succeeded = MappingErrorCode.None,
    LocalFileSystemAccessFailed = MappingErrorCode.LocalFileSystemAccessFailed,
    LocalVolumeNotSupported = MappingErrorCode.LocalVolumeNotSupported,
    LocalFolderDoesNotExist = MappingErrorCode.LocalFolderDoesNotExist,
    LocalFileDoesNotExist = MappingErrorCode.LocalFileDoesNotExist,
    LocalFolderNotEmpty = MappingErrorCode.LocalFolderNotEmpty,
    FolderIncludedByAnAlreadySyncedFolder = MappingErrorCode.LocalFolderIncludedByAnAlreadySyncedFolder,
    FolderIncludesAnAlreadySyncedFolder = MappingErrorCode.LocalFolderIncludesAnAlreadySyncedFolder,
    NonSyncableFolder = MappingErrorCode.LocalFolderNonSyncable,
    InsufficientLocalFreeSpace = MappingErrorCode.InsufficientLocalFreeSpace,
    NetworkFolderNotSupported,
}
