namespace Proton.Drive.App.Photos.Import;

internal interface IPhotoAlbumNameProvider
{
    string GetAlbumNameFromPath(string folderPath, ReadOnlySpan<char> rootFolderPath, ReadOnlySpan<char> relativeFolderPath);
}
