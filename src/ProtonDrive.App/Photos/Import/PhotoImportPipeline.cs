using Microsoft.Extensions.Logging;
using ProtonDrive.Client;
using ProtonDrive.Client.FileUploading;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.FileSystem.Metadata.LivePhoto;
using ProtonDrive.Sync.Shared.FileSystem.Photos;

namespace ProtonDrive.App.Photos.Import;

internal sealed class PhotoImportPipeline
{
    private readonly PhotoImportPipelineParameters _parameters;
    private readonly IPhotoFileSystemClient<long> _localFileSystemClient;
    private readonly IPhotoFileUploader _photoFileUploader;
    private readonly IPhotoAlbumService _photoAlbumService;
    private readonly IPhotoDuplicateService _duplicateService;
    private readonly IPhotoAlbumNameProvider _photoAlbumNameProvider;
    private readonly ILivePhotoFileDetector _livePhotoFileDetector;
    private readonly ImportProgress _progress;
    private readonly ILogger _logger;

    public PhotoImportPipeline(
        PhotoImportPipelineParameters parameters,
        IPhotoFileSystemClient<long> localFileSystemClient,
        IPhotoFileUploader photoFileUploader,
        IPhotoAlbumService photoAlbumService,
        IPhotoDuplicateService duplicateService,
        IPhotoAlbumNameProvider photoAlbumNameProvider,
        ILivePhotoFileDetector livePhotoFileDetector,
        ImportProgress progress,
        ILogger logger)
    {
        _parameters = parameters;
        _localFileSystemClient = localFileSystemClient;
        _photoFileUploader = photoFileUploader;
        _photoAlbumService = photoAlbumService;
        _duplicateService = duplicateService;
        _photoAlbumNameProvider = photoAlbumNameProvider;
        _livePhotoFileDetector = livePhotoFileDetector;
        _progress = progress;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Breadth enumeration (folder-by-folder, alphabetically) is used to take care of albums creation.
            var folders = _localFileSystemClient.EnumerateFoldersAsync(NodeInfo<long>.Directory().WithPath(_parameters.FolderPath), cancellationToken);

            await foreach (var folder in folders.ConfigureAwait(false))
            {
                if (AlbumIsAlreadyImported(folder.Path))
                {
                    var numberOfAlreadyImportedFiles =
                        await _localFileSystemClient.EnumeratePhotoFilesAsync(folder, cancellationToken).CountAsync(cancellationToken).ConfigureAwait(false);
                    _progress.RaiseFilesImported(numberOfAlreadyImportedFiles);
                    continue;
                }

                var photoAlbumImporter = new PhotoAlbumImporter(
                    _parameters,
                    _localFileSystemClient,
                    _photoFileUploader,
                    _photoAlbumService,
                    _duplicateService,
                    _photoAlbumNameProvider,
                    _livePhotoFileDetector,
                    _progress,
                    _logger);

                await photoAlbumImporter.CreateAlbumAndImportPhotosAsync(folder, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is FileSystemClientException || exception.IsDriveClientException())
        {
            throw new PhotoImportException("Import failed due to enumeration failure", exception);
        }
    }

    private static int GetFolderDepth(ReadOnlySpan<char> path)
    {
        return path.Count(Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// Skip folder that come before the resume point:
    /// <list type="bullet">
    /// <item>Skip if the folder is at a higher level in the directory tree (i.e. has fewer nested segments than the resume folder).</item>
    /// <item>Skip if the folder path is alphabetically before the resume folder path.
    /// This ensures we skip sibling or descendant folders that were already imported.</item>
    /// </list>
    /// </summary>
    /// <param name="albumFolderPath">Path of the folder to check.</param>
    /// <returns>Whether the folder has already been imported or not.</returns>
    private bool AlbumIsAlreadyImported(ReadOnlySpan<char> albumFolderPath)
    {
        if (!_parameters.FolderCurrentPosition.HasValue)
        {
            return false;
        }

        ReadOnlySpan<char> lastProcessedAlbumFolderPath = Path.Combine(
            _parameters.FolderPath,
            _parameters.FolderCurrentPosition.Value.RelativePath);

        if (GetFolderDepth(lastProcessedAlbumFolderPath) > GetFolderDepth(albumFolderPath))
        {
            return true; // Skipping due to depth
        }

        if (lastProcessedAlbumFolderPath.CompareTo(albumFolderPath, StringComparison.OrdinalIgnoreCase) > 0)
        {
            return true; // Skipping due to name
        }

        return false;
    }
}
