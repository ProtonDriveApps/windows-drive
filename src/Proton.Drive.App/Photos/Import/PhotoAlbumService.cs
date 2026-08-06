using Microsoft.Extensions.Logging;
using Proton.Drive.App.Photos.Albums;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;

namespace Proton.Drive.App.Photos.Import;

internal sealed class PhotoAlbumService : IPhotoAlbumService
{
    private readonly IFileSystemClient<string> _remoteFileSystemClient;
    private readonly IPhotoAlbumDuplicateService _photoAlbumDuplicationService;
    private readonly ILogger<PhotoAlbumService> _logger;

    public PhotoAlbumService(
        IFileSystemClient<string> remoteFileSystemClient,
        IPhotoAlbumDuplicateService photoAlbumDuplicationService,
        ILogger<PhotoAlbumService> logger)
    {
        _remoteFileSystemClient = remoteFileSystemClient;
        _photoAlbumDuplicationService = photoAlbumDuplicationService;
        _logger = logger;
    }

    public async ValueTask<string> CreateAlbumAsync(string albumName, string parentLinkId, CancellationToken cancellationToken)
    {
        try
        {
            var albumInfo = NodeInfo<string>.Directory()
                .WithName(albumName)
                .WithParentId(parentLinkId);

            var album = await _remoteFileSystemClient.CreateDirectoryAsync(albumInfo, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(album.Id))
            {
                throw new FileSystemClientException("Album creation failed: missing ID", FileSystemErrorCode.Unknown);
            }

            _logger.LogInformation("Created album with ID {ID}", album.Id);

            return album.Id;
        }
        catch (FileSystemClientException exception) when (exception.ErrorCode is FileSystemErrorCode.TooManyChildren)
        {
            throw new PhotoAlbumCreationException(
                $"Creation of Album with name '{albumName}' failed: limit reached",
                exception,
                PhotoImportErrorCode.MaximumNumberOfAlbumsReached);
        }
    }

    public async ValueTask AddToAlbumAsync(string albumLinkId, IReadOnlyList<NodeInfo<string>> files, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogDebug("Adding batch of {Count} photo files to album with ID {AlbumId}", files.Count, albumLinkId);

        var albumNode = new NodeInfo<string>().WithParentId(albumLinkId);
        await _remoteFileSystemClient.MoveAsync(files, albumNode, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Finished adding batch of {Count} photo files to album with ID {AlbumId}", files.Count, albumLinkId);
    }

    public ValueTask<string?> FindDuplicateAlbumAsync(string volumeId, string shareId, string albumName, CancellationToken cancellationToken)
    {
        return _photoAlbumDuplicationService.FindDuplicateAlbumIdAsync(volumeId, shareId, albumName, cancellationToken);
    }
}
