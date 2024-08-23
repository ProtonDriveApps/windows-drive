namespace ProtonDrive.App.Mapping;

public enum MappingErrorCode
{
    None,
    DriveAccessFailed,
    DriveVolumeDiverged,
    DriveShareDiverged,
    DriveHostDeviceDiverged,
    DriveFolderDoesNotExist,
    DriveFolderDiverged,
    LocalFileSystemAccessFailed,
    LocalVolumeNotSupported,
    LocalFolderDoesNotExist,
    LocalFileDoesNotExist,
    LocalFolderDiverged,
    LocalAndRemoteFoldersNotEmpty,
    LocalFolderNotEmpty,
    LocalFolderIncludedByAnAlreadySyncedFolder,
    LocalFolderIncludesAnAlreadySyncedFolder,
    LocalFolderNonSyncable,
    InsufficientLocalFreeSpace,
    SharingDisabled,
}
