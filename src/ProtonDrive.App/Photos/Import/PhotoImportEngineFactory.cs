using Microsoft.Extensions.Logging;
using ProtonDrive.App.Volumes;
using ProtonDrive.Client.FileUploading;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.FileSystem.Photos;

namespace ProtonDrive.App.Photos.Import;

internal sealed class PhotoImportEngineFactory : IPhotoImportEngineFactory
{
    private readonly IRemoteFileSystemClientFactory _remoteFileSystemClientFactory;
    private readonly ILocalFileSystemClientFactory _localFileSystemClientFactory;
    private readonly PhotoFileImporterFactory _photoFileImporterFactory;
    private readonly PhotoAlbumServiceFactory _photoAlbumServiceFactory;
    private readonly IPhotoDuplicateService _duplicateService;
    private readonly IPhotoAlbumNameProvider _photoAlbumNameProvider;
    private readonly ILivePhotoFileDetector _livePhotoFileDetector;
    private readonly ILoggerFactory _loggerFactory;
    private readonly int _maxNumberOfConcurrentFileTransfers;

    public PhotoImportEngineFactory(
        IRemoteFileSystemClientFactory remoteFileSystemClientFactory,
        ILocalFileSystemClientFactory localFileSystemClientFactory,
        PhotoFileImporterFactory photoFileImporterFactory,
        PhotoAlbumServiceFactory photoAlbumServiceFactory,
        IPhotoDuplicateService duplicateService,
        IPhotoAlbumNameProvider photoAlbumNameProvider,
        ILivePhotoFileDetector livePhotoFileDetector,
        AppConfig appConfig,
        ILoggerFactory loggerFactory)
    {
        _maxNumberOfConcurrentFileTransfers = appConfig.MaxNumberOfConcurrentFileTransfers;
        _remoteFileSystemClientFactory = remoteFileSystemClientFactory;
        _localFileSystemClientFactory = localFileSystemClientFactory;
        _photoFileImporterFactory = photoFileImporterFactory;
        _photoAlbumServiceFactory = photoAlbumServiceFactory;
        _duplicateService = duplicateService;
        _photoAlbumNameProvider = photoAlbumNameProvider;
        _livePhotoFileDetector = livePhotoFileDetector;
        _loggerFactory = loggerFactory;
    }

    public IPhotoImportEngine CreateEngine(PhotoImportFolderState folder, VolumeInfo photoVolume)
    {
        var remoteFileSystemClient = _remoteFileSystemClientFactory.CreateClient(
            new FileSystemClientParameters(photoVolume.Id, photoVolume.RootShareId, IsPhotoClient: true));

        var photoAlbumService = _photoAlbumServiceFactory.CreatePhotoAlbumService(remoteFileSystemClient);

        return new PhotoImportEngine(
            folder,
            photoVolume,
            remoteFileSystemClient,
            _localFileSystemClientFactory,
            _photoFileImporterFactory,
            photoAlbumService,
            _duplicateService,
            _photoAlbumNameProvider,
            _livePhotoFileDetector,
            _maxNumberOfConcurrentFileTransfers,
            _loggerFactory.CreateLogger<PhotoImportEngine>());
    }
}
