using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.App.Photos.Import;

internal interface IPhotoAlbumService
{
    ValueTask<string> CreateAlbumAsync(string albumName, string parentLinkId, CancellationToken cancellationToken);

    ValueTask AddToAlbumAsync(string albumLinkId, IReadOnlyList<NodeInfo<string>> files, CancellationToken cancellationToken);

    ValueTask<string?> FindDuplicateAlbumAsync(string volumeId, string shareId, string albumName, CancellationToken cancellationToken);
}
