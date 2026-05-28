namespace ProtonDrive.Sync.Shared.FileSystem.Photos;

public enum PhotoImportErrorCode
{
    Unknown,
    FolderDoesNotExist,
    AlbumDoesNotExist,
    MaximumNumberOfAlbumsReached,
    MaximumNumberOfPhotosPerAlbumReached,
}
