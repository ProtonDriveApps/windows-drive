using Microsoft.Extensions.Logging;
using Proton.Drive.App.Photos.Albums;
using Proton.Drive.Sdk.Sync.Client;
using Proton.Drive.Sdk.Sync.Client.Albums;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.App.Photos.Import;

internal sealed class PhotoAlbumServiceFactory
{
    private readonly IPhotoApiClient _photoApiClient;
    private readonly IAlbumNodeService _albumNodeService;
    private readonly ILoggerFactory _loggerFactory;

    public PhotoAlbumServiceFactory(IPhotoApiClient photoApiClient, IAlbumNodeService albumNodeService, ILoggerFactory loggerFactory)
    {
        _photoApiClient = photoApiClient;
        _albumNodeService = albumNodeService;
        _loggerFactory = loggerFactory;
    }

    public IPhotoAlbumService CreatePhotoAlbumService(IFileSystemClient<string> remoteFileSystemClient)
    {
        var albumDuplicateService = new PhotoAlbumDuplicateService(
            _photoApiClient,
            _albumNodeService,
            _loggerFactory.CreateLogger<PhotoAlbumDuplicateService>());

        return new PhotoAlbumService(
            remoteFileSystemClient,
            albumDuplicateService,
            _loggerFactory.CreateLogger<PhotoAlbumService>());
    }
}
