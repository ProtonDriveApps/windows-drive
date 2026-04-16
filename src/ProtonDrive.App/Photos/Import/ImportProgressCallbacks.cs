namespace ProtonDrive.App.Photos.Import;

internal sealed class ImportProgressCallbacks
{
    public Action<int, int>? OnProgressChanged { get; init; }

    public Action<PhotoImportFolderCurrentPosition>? OnAlbumSelected { get; init; }

    public Action<string, Exception?>? OnPhotoFileActivityChanged { get; init; }
}
