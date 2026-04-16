using System.Security;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Settings;
using ProtonDrive.Client.FileUploading;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.FileSystem.Photos;

namespace ProtonDrive.App.Photos.Import;

internal sealed class PhotoImportEngine : IPhotoImportEngine
{
    private const int DuplicationCheckBatchSize = 150; // Maximum batch size allowed by the duplication check API

    private readonly string _volumeId;
    private readonly string _shareId;
    private readonly string _rootLinkId;
    private readonly string _folderPath;
    private readonly PhotoImportFolderCurrentPosition? _folderCurrentPosition;
    private readonly IFileSystemClient<string> _remoteFileSystemClient;
    private readonly ILocalFileSystemClientFactory _localFileSystemClientFactory;
    private readonly PhotoFileImporterFactory _photoFileImporterFactory;
    private readonly IPhotoAlbumService _photoAlbumService;
    private readonly IPhotoDuplicateService _duplicateService;
    private readonly IPhotoAlbumNameProvider _photoAlbumNameProvider;
    private readonly ILivePhotoFileDetector _livePhotoFileDetector;
    private readonly int _maxNumberOfConcurrentFileTransfers;
    private readonly ILogger<PhotoImportEngine> _logger;

    public PhotoImportEngine(
        RemoteToLocalMapping mapping,
        PhotoImportFolderCurrentPosition? folderCurrentPosition,
        IFileSystemClient<string> remoteFileSystemClient,
        ILocalFileSystemClientFactory localFileSystemClientFactory,
        PhotoFileImporterFactory photoFileImporterFactory,
        IPhotoAlbumService photoAlbumService,
        IPhotoDuplicateService duplicateService,
        IPhotoAlbumNameProvider photoAlbumNameProvider,
        ILivePhotoFileDetector livePhotoFileDetector,
        int maxNumberOfConcurrentFileTransfers,
        ILogger<PhotoImportEngine> logger)
    {
        _volumeId = mapping.Remote.VolumeId ?? throw new ArgumentNullException(nameof(mapping), "Volume ID is required");
        _shareId = mapping.Remote.ShareId ?? throw new ArgumentNullException(nameof(mapping), "Share ID is required");
        _rootLinkId = mapping.Remote.RootLinkId ?? throw new ArgumentNullException(nameof(mapping), "Root link ID is required");
        _folderPath = mapping.Local.Path;
        _folderCurrentPosition = folderCurrentPosition;
        _remoteFileSystemClient = remoteFileSystemClient;
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
            _volumeId,
            _shareId,
            _rootLinkId,
            _folderPath,
            _folderCurrentPosition,
            _maxNumberOfConcurrentFileTransfers,
            DuplicationCheckBatchSize);

        return ImportInternalAsync(parameters, callbacks, cancellationToken);
    }

    private async Task ImportInternalAsync(PhotoImportPipelineParameters parameters, ImportProgressCallbacks callbacks, CancellationToken cancellationToken)
    {
        var localFileSystemClient = _localFileSystemClientFactory.CreatePhotoClient();
        var rootInfo = NodeInfo<long>.Directory().WithPath(parameters.FolderPath);
        var progress = new ImportProgress(callbacks);

        // First pass: enumerate the folder to count how many files need to be imported.
        // This allows us to display progress without loading all file paths into memory.
        var countingTask = CountFilesToImportAsync(localFileSystemClient.EnumerateAllPhotoFilesAsync(rootInfo, cancellationToken), progress, cancellationToken);
        await countingTask.ConfigureAwait(false);

        var importPipeline = new PhotoImportPipeline(
            parameters,
            localFileSystemClient,
            _photoFileImporterFactory.Create(localFileSystemClient, _remoteFileSystemClient, _volumeId),
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            throw new PhotoImportException($"Photo import counting failed on folder \"{_logger.GetSensitiveValueForLogging(_folderPath)}\"", ex);
        }
    }
}
