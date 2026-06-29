namespace Proton.Drive.Sdk.Sync.Agent.Mapping;

public enum SyncFolderValidationResult
{
    Succeeded = MappingErrorCode.None,
    LocalFileSystemAccessFailed = MappingErrorCode.LocalFileSystemAccessFailed,
    LocalVolumeNotSupported = MappingErrorCode.LocalVolumeNotSupported,
    LocalFolderDoesNotExist = MappingErrorCode.LocalFolderDoesNotExist,
    LocalFolderNotEmpty = MappingErrorCode.LocalFolderNotEmpty,
    FolderIncludedByAnAlreadySyncedFolder = MappingErrorCode.LocalFolderIncludedByAnAlreadySyncedFolder,
    FolderIncludesAnAlreadySyncedFolder = MappingErrorCode.LocalFolderIncludesAnAlreadySyncedFolder,
    NonSyncableFolder = MappingErrorCode.LocalFolderNonSyncable,
    InsufficientLocalFreeSpace = MappingErrorCode.InsufficientLocalFreeSpace,
    NetworkFolderNotSupported,
}
