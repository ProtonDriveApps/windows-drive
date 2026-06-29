using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Volumes;
using Proton.Drive.Sdk.Sync.Client.FileUploading;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Metadata.LivePhoto;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;
using Proton.Drive.Shared.Logging;

namespace Proton.Drive.App.Photos.Import;

internal sealed class PhotoImportEngine : IPhotoImportEngine
{
    private const int DuplicationCheckBatchSize = 150; // Maximum batch size allowed by the duplication check API

    private readonly PhotoImportFolderState _folder;
    private readonly VolumeInfo _photoVolume;
    private readonly PhotoImportFolderCurrentPosition? _folderCurrentPosition;
    private readonly ILocalFileSystemClientFactory _localFileSystemClientFactory;
    private readonly PhotoFileImporterFactory _photoFileImporterFactory;
    private readonly IPhotoAlbumService _photoAlbumService;
    private readonly IPhotoDuplicateService _duplicateService;
    private readonly IPhotoAlbumNameProvider _photoAlbumNameProvider;
    private readonly ILivePhotoFileDetector _livePhotoFileDetector;
    private readonly int _maxNumberOfConcurrentFileTransfers;
    private readonly ILogger<PhotoImportEngine> _logger;

    public PhotoImportEngine(
        PhotoImportFolderState folder,
        VolumeInfo photoVolume,
        ILocalFileSystemClientFactory localFileSystemClientFactory,
        PhotoFileImporterFactory photoFileImporterFactory,
        IPhotoAlbumService photoAlbumService,
        IPhotoDuplicateService duplicateService,
        IPhotoAlbumNameProvider photoAlbumNameProvider,
        ILivePhotoFileDetector livePhotoFileDetector,
        int maxNumberOfConcurrentFileTransfers,
        ILogger<PhotoImportEngine> logger)
    {
        _folder = folder;
        _photoVolume = photoVolume;
        _folderCurrentPosition = _folder.CurrentPosition;
        _localFileSystemClientFactory = localFileSystemClientFactory;
        _photoFileImporterFactory = photoFileImporterFactory;
        _photoAlbumService = photoAlbumService;
        _duplicateService = duplicateService;
        _photoAlbumNameProvider = photoAlbumNameProvider;
        _livePhotoFileDetector = livePhotoFileDetector;
        _maxNumberOfConcurrentFileTransfers = maxNumberOfConcurrentFileTransfers;
        _logger = logger;
    }

    public Task ImportAsync(ImportProgressCallbacks callbacks, CancellationToken cancellationToken)
    {
        var parameters = new PhotoImportPipelineParameters(
            _photoVolume.Id,
            _photoVolume.RootShareId,
            _photoVolume.RootLinkId,
            _folder.Path,
            _folderCurrentPosition,
            _maxNumberOfConcurrentFileTransfers,
            DuplicationCheckBatchSize);

        return ImportInternalAsync(parameters, callbacks, cancellationToken);
    }

    private async Task ImportInternalAsync(PhotoImportPipelineParameters parameters, ImportProgressCallbacks callbacks, CancellationToken cancellationToken)
    {
        var localFileSystemClient = _localFileSystemClientFactory.CreatePhotoClient();
        var rootFolder = NodeInfo<long>.Directory().WithPath(parameters.FolderPath);
        var progress = new ImportProgress(callbacks);

        await ValidateFolderAsync(() => localFileSystemClient.GetInfoAsync(rootFolder, cancellationToken)).ConfigureAwait(false);

        // First pass: enumerate the folder to count how many files need to be imported.
        // This allows us to display progress without loading all file paths into memory.
        var countingTask = CountFilesToImportAsync(localFileSystemClient.EnumerateAllPhotoFilesAsync(rootFolder, cancellationToken), progress, cancellationToken);
        await countingTask.ConfigureAwait(false);

        var importPipeline = new PhotoImportPipeline(
            parameters,
            localFileSystemClient,
            _photoFileImporterFactory.Create(localFileSystemClient, _photoVolume.Id),
            _photoAlbumService,
            _duplicateService,
            _photoAlbumNameProvider,
            _livePhotoFileDetector,
            progress,
            _logger);

        // Second pass: enumerate the folder again to perform the actual import.
        // This approach minimizes memory usage by avoiding storing all file paths at once.
        await importPipeline.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ValidateFolderAsync(Func<Task<NodeInfo<long>>> getInfoTask)
    {
        try
        {
            await getInfoTask.Invoke().ConfigureAwait(false);
        }
        catch (FileSystemClientException ex)
        {
            if (ex.ErrorCode is FileSystemErrorCode.DirectoryNotFound or FileSystemErrorCode.PathNotFound)
            {
                throw new PhotoImportException(
                    $"Folder \"{_logger.GetSensitiveValueForLogging(_folder.Path)}\" does not exist",
                    PhotoImportErrorCode.FolderDoesNotExist,
                    ex);
            }

            throw new PhotoImportException($"Folder \"{_logger.GetSensitiveValueForLogging(_folder.Path)}\" validation failed", ex);
        }
    }

    private async Task CountFilesToImportAsync(
        IAsyncEnumerable<NodeInfo<long>> nodes,
        ImportProgress progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await foreach (var node in nodes.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                progress.RaiseFileToImportFound();
            }
        }
        catch (FileSystemClientException ex)
        {
            throw new PhotoImportException($"Photos counting failed on folder \"{_logger.GetSensitiveValueForLogging(_folder.Path)}\"", ex);
        }
    }
}
