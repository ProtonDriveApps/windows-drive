namespace Proton.Drive.App.Photos.Albums;

public interface IPhotoAlbumDuplicateService
{
    ValueTask<string?> FindDuplicateAlbumIdAsync(string volumeId, string shareId, string albumName, CancellationToken cancellationToken);
}
