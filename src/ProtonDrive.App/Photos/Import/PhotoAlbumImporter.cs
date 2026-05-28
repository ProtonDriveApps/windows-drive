using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MoreLinq;
using ProtonDrive.Client;
using ProtonDrive.Client.FileUploading;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.FileSystem.Photos;

namespace ProtonDrive.App.Photos.Import;

internal sealed class PhotoAlbumImporter
{
    private const int MaxAddToAlbumBatchSize = 100;

    private readonly PhotoImportPipelineParameters _parameters;
    private readonly IPhotoFileSystemClient<long> _localFileSystemClient;
    private readonly IPhotoFileUploader _photoFileUploader;
    private readonly IPhotoAlbumService _photoAlbumService;
    private readonly IPhotoDuplicateService _duplicateService;
    private readonly IPhotoAlbumNameProvider _albumNameProvider;
    private readonly ILivePhotoFileDetector _livePhotoFileDetector;
    private readonly ImportProgress _progress;
    private readonly ILogger _logger;

    private readonly SemaphoreSlim _semaphore;

    public PhotoAlbumImporter(
        PhotoImportPipelineParameters parameters,
        IPhotoFileSystemClient<long> localFileSystemClient,
        IPhotoFileUploader photoFileUploader,
        IPhotoAlbumService photoAlbumService,
        IPhotoDuplicateService duplicateService,
        IPhotoAlbumNameProvider albumNameProvider,
        ILivePhotoFileDetector livePhotoFileDetector,
        ImportProgress progress,
        ILogger logger)
    {
        _parameters = parameters;
        _localFileSystemClient = localFileSystemClient;
        _photoFileUploader = photoFileUploader;
        _photoAlbumService = photoAlbumService;
        _albumNameProvider = albumNameProvider;
        _livePhotoFileDetector = livePhotoFileDetector;
        _duplicateService = duplicateService;
        _progress = progress;
        _logger = logger;

        _semaphore = new SemaphoreSlim(_parameters.MaxNumberOfConcurrentFileTransfers);
    }

    public async Task CreateAlbumAndImportPhotosAsync(NodeInfo<long> albumFolder, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var batch = new List<PhotoGroup>(_parameters.DuplicationCheckBatchSize);
        var currentBatchCount = 0;

        var photoFiles = _localFileSystemClient.EnumeratePhotoFilesAsync(albumFolder, cancellationToken);
        var photoGroups = PhotoGroupEnumerator.EnumerateAsync(photoFiles, _livePhotoFileDetector);
        var albumIdTask = new Lazy<Task<string>>(() => CreateOrResumeAlbumAsync(albumFolder.Path, cancellationToken));

        await foreach (var photoGroup in photoGroups.ConfigureAwait(false))
        {
            if (currentBatchCount + photoGroup.Count > _parameters.DuplicationCheckBatchSize)
            {
                await ProcessBatchAsync(albumIdTask, batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
                currentBatchCount = 0;
            }

            batch.Add(photoGroup);
            currentBatchCount += photoGroup.Count;
        }

        await ProcessBatchAsync(albumIdTask, batch, cancellationToken).ConfigureAwait(false);
        batch.Clear();
    }

    private static bool IsExpectedException(Exception exception)
    {
        return exception.IsFileAccessException() || exception.IsDriveClientException() || exception is FileSystemClientException;
    }

    private bool TryGetPreviouslyUsedAlbumLinkId(string albumFolderPath, [MaybeNullWhen(false)] out string albumLinkId)
    {
        if (_parameters.FolderCurrentPosition is not { } currentPosition)
        {
            albumLinkId = null;
            return false;
        }

        var lastProcessedAlbumPath = Path.Combine(_parameters.FolderPath, currentPosition.RelativePath);

        if (!string.Equals(albumFolderPath, lastProcessedAlbumPath, StringComparison.OrdinalIgnoreCase))
        {
            albumLinkId = null;
            return false;
        }

        albumLinkId = currentPosition.AlbumLinkId;

        var folderPathToLog = _logger.GetSensitiveValueForLogging(currentPosition.RelativePath);
        _logger.LogInformation("Photo import resumed: Folder \"{Path}\", Album ID {AlbumLinkId}", folderPathToLog, albumLinkId);
        return true;
    }

    private async Task ProcessBatchAsync(Lazy<Task<string>> albumLinkIdTask, IReadOnlyList<PhotoGroup> batch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (batch.Count == 0)
        {
            return;
        }

        var albumLinkId = await albumLinkIdTask.Value.ConfigureAwait(false);

        var fileNames = batch.SelectMany(pg => pg.RelatedMedia.Select(x => x.File.Name).Prepend(pg.MainPhoto.File.Name));

        var nameCollisions = await _duplicateService.GetNameCollisionsAsync(
            _parameters.VolumeId,
            _parameters.ShareId,
            _parameters.ParentLinkId,
            fileNames,
            cancellationToken).ConfigureAwait(false);

        try
        {
            var importPhotoTasks = new List<Task<IReadOnlyList<NodeInfo<string>>>>(_parameters.MaxNumberOfConcurrentFileTransfers);

            foreach (var photoGroup in batch)
            {
                await MarkDuplicatesAsync(photoGroup, nameCollisions, cancellationToken).ConfigureAwait(false);

                await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                var importTask = ImportPhotoGroupAsync(photoGroup, cancellationToken);

                importPhotoTasks.Add(importTask);
            }

            var batchesToAddToAlbum = (await Task.WhenAll(importPhotoTasks).ConfigureAwait(false))
                .SelectMany(x => x)
                .Batch(MaxAddToAlbumBatchSize);

            foreach (var batchToAddToAlbum in batchesToAddToAlbum)
            {
                _logger.LogDebug("Adding batch of {Count} photos to album", batchToAddToAlbum.Length);

                await _photoAlbumService.AddToAlbumAsync(albumLinkId, batchToAddToAlbum, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (AggregateException exception)
        {
            exception.Handle(IsExpectedException);

            throw new PhotoImportException($"Processing batch of {batch.Count} photos failed", exception);
        }
        catch (Exception exception) when (IsExpectedException(exception))
        {
            throw new PhotoImportException($"Processing batch of {batch.Count} photos failed", exception);
        }
    }

    private async Task<string> CreateOrResumeAlbumAsync(string albumFolderPath, CancellationToken cancellationToken)
    {
        if (TryGetPreviouslyUsedAlbumLinkId(albumFolderPath, out var previouslyUsedAlbumLinkId))
        {
            return previouslyUsedAlbumLinkId;
        }

        var rootFolderPath = _parameters.FolderPath.AsSpan();
        var currentFolderPath = albumFolderPath.AsSpan();

        if (!currentFolderPath.StartsWith(rootFolderPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new PhotoImportException("Album name cannot be created: Root folder path and current folder path are not related");
        }

        var relativePath = (currentFolderPath.Length != rootFolderPath.Length
            ? currentFolderPath[(rootFolderPath.Length + 1)..]
            : ReadOnlySpan<char>.Empty).ToString();

        var albumName = _albumNameProvider.GetAlbumNameFromPath(albumFolderPath, rootFolderPath, relativePath);

        var albumLinkId = await GetAlbumLinkIdAsync(albumFolderPath, albumName, cancellationToken).ConfigureAwait(false);

        _progress.RaiseAlbumSelected(new PhotoImportFolderCurrentPosition { AlbumLinkId = albumLinkId, RelativePath = relativePath });

        return albumLinkId;
    }

    private async Task<string> GetAlbumLinkIdAsync(string albumFolderPath, string albumName, CancellationToken cancellationToken)
    {
        var duplicateAlbumLinkId = await _photoAlbumService.FindDuplicateAlbumAsync(_parameters.VolumeId, _parameters.ShareId, albumName, cancellationToken)
            .ConfigureAwait(false);

        if (duplicateAlbumLinkId is not null)
        {
            var albumFolderPathToLog = _logger.GetSensitiveValueForLogging(albumFolderPath);
            _logger.LogInformation("Existing album found: Folder \"{Path}\", Album ID {AlbumLinkId}", albumFolderPathToLog, duplicateAlbumLinkId);

            return duplicateAlbumLinkId;
        }

        return await _photoAlbumService.CreateAlbumAsync(albumName, _parameters.ParentLinkId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<NodeInfo<string>>> ImportPhotoGroupAsync(PhotoGroup photoGroup, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var photosToAddToAlbum = new List<NodeInfo<string>>(1 + photoGroup.RelatedMedia.Count);

            var uploadedFile = photoGroup.MainPhoto.Duplicate
                ?? await UploadFileAsync(photoGroup.MainPhoto.File.Path, null, cancellationToken).ConfigureAwait(false);

            _progress.RaiseFileImported();

            photosToAddToAlbum.Add(uploadedFile);

            if (photoGroup.RelatedMedia.Count > 0)
            {
                foreach (var relatedMedia in photoGroup.RelatedMedia)
                {
                    var uploadedRelatedFile = relatedMedia.Duplicate
                        ?? await UploadFileAsync(relatedMedia.File.Path, uploadedFile.Id, cancellationToken).ConfigureAwait(false);

                    _progress.RaiseFileImported();

                    photosToAddToAlbum.Add(uploadedRelatedFile);
                }
            }

            return photosToAddToAlbum;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<NodeInfo<string>> UploadFileAsync(string filePath, string? mainPhotoLinkId, CancellationToken cancellationToken)
    {
        const int maxNumberOfRetries = 2;

        var filePathToLog = _logger.GetSensitiveValueForLogging(filePath);
        var attempt = 1;

        while (true)
        {
            try
            {
                _logger.LogInformation("Uploading file \"{Path}\" (attempt {Attempt}/{MaxNumberOfRetries})", filePathToLog, attempt, maxNumberOfRetries);

                var importedFile = await _photoFileUploader.UploadFileAsync(filePath, _parameters.ParentLinkId, mainPhotoLinkId, cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogInformation("File \"{Path}\" uploaded", filePathToLog);

                _progress.RaiseFileUploaded(filePath);

                return importedFile;
            }
            catch (PhotoFileSizeMismatchException exception) when (attempt < maxNumberOfRetries)
            {
                ++attempt;

                _progress.RaiseFileUploadFailed(filePath, exception);
            }
            catch (PhotoFileSizeMismatchException exception)
            {
                _progress.RaiseFileUploadFailed(filePath, exception);

                throw;
            }
            catch (FileSystemClientException exception) when (exception.ErrorCode is FileSystemErrorCode.MainPhotoAlreadyInAlbum)
            {
                if (mainPhotoLinkId is null)
                {
                    throw;
                }

                // The related photo cannot be uploaded as part of a Live Photo because its main photo already belongs to an album.
                // The upload will be retried once as a normal, independent photo.
                mainPhotoLinkId = null;
                _logger.LogInformation(
                    "Failed to upload the related photo \"{Path}\": {Message}. {Details}",
                    filePathToLog,
                    exception.CombinedMessage(),
                    "Retry uploading file as independent photo");
            }
            catch (Exception exception) when (IsExpectedException(exception))
            {
                // TODO: Avoid log-and-throw (antipattern)
                _logger.LogWarning("Failed to upload file \"{Path}\": {Message}", filePathToLog, exception.CombinedMessage());

                _progress.RaiseFileUploadFailed(filePath, exception);

                throw;
            }
        }
    }

    private async Task MarkDuplicatesAsync(
        PhotoGroup photoGroup,
        ILookup<string, PhotoNameCollision> nameCollisions,
        CancellationToken cancellationToken)
    {
        var fileHashCache = new Dictionary<string, (string ContentHash, string Sha1Digest)>();

        await MarkIfDuplicateAsync(photoGroup.MainPhoto, nameCollisions, fileHashCache, cancellationToken).ConfigureAwait(false);

        foreach (var relatedMedium in photoGroup.RelatedMedia)
        {
            await MarkIfDuplicateAsync(relatedMedium, nameCollisions, fileHashCache, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task MarkIfDuplicateAsync(
        PhotoImportInfo photoImportInfo,
        ILookup<string, PhotoNameCollision> nameCollisions,
        Dictionary<string, (string ContentHash, string Sha1Digest)> fileHashCache,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!nameCollisions.Contains(photoImportInfo.File.Name))
            {
                return;
            }

            // A file with the same name can be uploaded multiple times if its content differs.
            // Therefore, when checking for duplicates, we must evaluate all matching filenames.
            foreach (var collision in nameCollisions[photoImportInfo.File.Name])
            {
                var filePathToLog = _logger.GetSensitiveValueForLogging(photoImportInfo.File.Path);

                if (collision.ContentHash is null)
                {
                    // The draft will be ignored and the file will be re-uploaded.
                    continue;
                }

                if (!fileHashCache.TryGetValue(photoImportInfo.File.Path, out var fileHashes))
                {
                    var source = await _localFileSystemClient.OpenFileForReadingAsync(NodeInfo<long>.File().WithPath(photoImportInfo.File.Path), cancellationToken)
                        .ConfigureAwait(false);

                    await using (source.ConfigureAwait(false))
                    {
                        var contentStream = source.GetContentStream();

                        await using (contentStream.ConfigureAwait(false))
                        {
                            fileHashes = await _duplicateService.GetContentHashAndSha1DigestAsync(
                                contentStream,
                                _parameters.ShareId,
                                _parameters.ParentLinkId,
                                cancellationToken).ConfigureAwait(false);

                            fileHashCache[photoImportInfo.File.Path] = fileHashes;
                        }
                    }
                }

                if (fileHashes.ContentHash.Equals(collision.ContentHash, StringComparison.Ordinal))
                {
                    _logger.LogInformation("Photo with path \"{Path}\" upload skipped, duplicate detected", filePathToLog);

                    photoImportInfo.MarkAsDuplicate(NodeInfo<string>.File()
                        .WithId(collision.LinkId)
                        .WithName(photoImportInfo.File.Name)
                        .WithSha1Digest(fileHashes.Sha1Digest));

                    return; // No need to check further collisions once we found a duplicate
                }
            }
        }
        catch (Exception exception) when (IsExpectedException(exception))
        {
            throw new PhotoImportException(
                $"Eligibility cannot be evaluated for file with path \"{_logger.GetSensitiveValueForLogging(photoImportInfo.File.Path)}\"",
                exception);
        }
    }

    private static class PhotoGroupEnumerator
    {
        public static async IAsyncEnumerable<PhotoGroup> EnumerateAsync(
            IAsyncEnumerable<NodeInfo<long>> photoFiles,
            ILivePhotoFileDetector livePhotoFileDetector)
        {
            PhotoImportInfo? currentMainFile = null;
            List<PhotoImportInfo> currentRelatedMediaFiles = [];

            await foreach (var nodeInfo in photoFiles.ConfigureAwait(false))
            {
                var file = NodeInfo<string>.File().WithPath(nodeInfo.Path).WithName(nodeInfo.Name);

                if (currentMainFile is null)
                {
                    currentMainFile = new PhotoImportInfo(file);
                    continue;
                }

                if (livePhotoFileDetector.IsVideoRelatedToLivePhoto(file.Path, currentMainFile.File.Path))
                {
                    currentRelatedMediaFiles.Add(new PhotoImportInfo(file));
                    continue;
                }

                yield return new PhotoGroup(currentMainFile, currentRelatedMediaFiles);

                currentMainFile = new PhotoImportInfo(file);
                currentRelatedMediaFiles = [];
            }

            if (currentMainFile is not null)
            {
                yield return new PhotoGroup(currentMainFile, currentRelatedMediaFiles);
            }
        }
    }

    private sealed record PhotoImportInfo(NodeInfo<string> File)
    {
        public NodeInfo<string>? Duplicate { get; private set; }

        public void MarkAsDuplicate(NodeInfo<string> duplicate)
        {
            Duplicate = duplicate;
        }
    }

    private record PhotoGroup(PhotoImportInfo MainPhoto, IReadOnlyList<PhotoImportInfo> RelatedMedia)
    {
        public int Count => RelatedMedia.Count + 1;
    }
}
