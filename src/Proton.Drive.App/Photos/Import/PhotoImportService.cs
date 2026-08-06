using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Proton.Drive.App.Photos.Volume;
using Proton.Drive.Sdk.Sync.Agent.Mapping.SyncFolders;
using Proton.Drive.Sdk.Sync.Agent.Services;
using Proton.Drive.Sdk.Sync.Agent.Volumes;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;
using Proton.Drive.Sdk.Sync.Shared.SyncActivity;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.IO;
using Proton.Drive.Shared.Logging;
using Proton.Drive.Shared.Telemetry;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.App.Photos.Import;

internal sealed class PhotoImportService : IStoppableService, IPhotoVolumeStateAware, IPhotoImportFoldersAware
{
    private readonly Lazy<IEnumerable<IPhotoImportActivityAware>> _photoImportActivityAware;
    private readonly IPhotoImportFolderService _photoFolderService;
    private readonly IPhotoImportEngineFactory _photoImportEngineFactory;
    private readonly IErrorCounter _errorCounter;
    private readonly ILogger<PhotoImportService> _logger;

    private readonly CoalescingAction _photoImport;
    private readonly SemaphoreSlim _currentFolderSemaphore = new(1, 1);
    private readonly StringIdMapper _stringIdMapper = new();

    private volatile bool _stopping;
    private volatile VolumeState _photoVolumeState = VolumeState.Idle;
    private volatile ImmutableList<PhotoImportFolderState> _folders = [];
    private volatile PhotoImportFolderState? _currentFolder;

    public PhotoImportService(
        Lazy<IEnumerable<IPhotoImportActivityAware>> photoImportActivityAware,
        IPhotoImportFolderService photoFolderService,
        IPhotoImportEngineFactory photoImportEngineFactory,
        IErrorCounter errorCounter,
        ILogger<PhotoImportService> logger)
    {
        _photoImportActivityAware = photoImportActivityAware;
        _photoFolderService = photoFolderService;
        _photoImportEngineFactory = photoImportEngineFactory;
        _errorCounter = errorCounter;
        _logger = logger;

        _photoImport = _logger.GetCoalescingActionWithExceptionsLoggingAndCancellationHandling(ImportAsync, nameof(PhotoImportService));
    }

    void IPhotoVolumeStateAware.OnPhotoVolumeStateChanged(VolumeState value)
    {
        _photoVolumeState = value;

        if (value.Status is VolumeStatus.Ready)
        {
            _photoImport.Run();
        }
        else
        {
            _photoImport.Cancel();
        }
    }

    void IPhotoImportFoldersAware.OnPhotoImportFolderChanged(SyncFolderChangeType changeType, PhotoImportFolderState folder)
    {
        if (_stopping)
        {
            return;
        }

        switch (changeType)
        {
            case SyncFolderChangeType.Added:
                _folders = _folders.Add(folder);

                if (_photoVolumeState.Status is VolumeStatus.Ready)
                {
                    _photoImport.Run();
                }

                break;

            case SyncFolderChangeType.Updated:
                if (folder == _currentFolder)
                {
                    break;
                }

                if (_photoVolumeState.Status is VolumeStatus.Ready)
                {
                    _photoImport.Run();
                }

                break;

            case SyncFolderChangeType.Removed:
                _folders = _folders.Remove(folder);
                StopImportingFolder(folder);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(changeType), changeType, null);
        }
    }

    async Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug($"{nameof(PhotoImportService)} is stopping");
        _stopping = true;
        _photoImport.Cancel();

        await WaitForCompletionAsync().ConfigureAwait(false);

        _logger.LogInformation($"{nameof(PhotoImportService)} stopped");
    }

    internal Task WaitForCompletionAsync()
    {
        return _photoImport.WaitForCompletionAsync();
    }

    private void StopImportingFolder(PhotoImportFolderState folder)
    {
        using (_currentFolderSemaphore.Lock())
        {
            if (folder == _currentFolder)
            {
                _photoImport.Cancel();
            }
        }
    }

    private async Task ImportAsync(CancellationToken cancellationToken)
    {
        var photoVolumeState = _photoVolumeState;

        if (photoVolumeState.Status is not VolumeStatus.Ready)
        {
            _logger.LogInformation("Photo import skipped, photo volume state is {VolumeStatus}", photoVolumeState.Status);
            return;
        }

        if (photoVolumeState.Volume is null)
        {
            _logger.LogWarning("Photo import skipped, photo volume not available");
            return;
        }

        var folders = _folders;

        foreach (var folder in folders)
        {
            try
            {
                using (await _currentFolderSemaphore.LockAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (folders != _folders)
                    {
                        // List of folders has changed, skipping remaining
                        return;
                    }

                    _currentFolder = folder;
                }

                if (folder.Status
                    is not PhotoImportFolderStatus.NotStarted
                    and not PhotoImportFolderStatus.Importing
                    and not PhotoImportFolderStatus.Interrupted)
                {
                    continue;
                }

                await ImportFolderAsync(folder, photoVolumeState.Volume, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _currentFolder = null;
            }
        }
    }

    private async Task ImportFolderAsync(PhotoImportFolderState folder, VolumeInfo photoVolume, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Photo import started: folder {FolderID} \"{Path}\"",
                folder.Id,
                _logger.GetSensitiveValueForLogging(folder.Path));

            OnStarted(folder);

            // A delay makes the folder status change noticeable in the UI even if the folder import immediately completes
            await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken).ConfigureAwait(false);

            var engine = _photoImportEngineFactory.CreateEngine(folder, photoVolume);
            var callbacks = GetProgressCallbacks(folder);

            await engine.ImportAsync(callbacks, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Photo import succeeded");
            OnSucceeded(folder);
        }
        catch (OperationCanceledException)
        {
            // TODO: Avoid log-and-throw (antipattern)
            _logger.LogInformation("Photo import interrupted");
            OnInterrupted(folder);
            throw;
        }
        catch (Exception exception) when (exception is PhotoImportException)
        {
            HandleFailure(exception);
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
            throw;
        }

        return;

        void HandleFailure(Exception exception)
        {
            _logger.LogWarning("Photo import failed: {ErrorMessage}", exception.CombinedMessage());

            folder.ErrorCode = exception is PhotoImportException photoImportException
                ? photoImportException.ErrorCode
                : PhotoImportErrorCode.Unknown;

            _errorCounter.Add(ErrorScope.PhotoImport, exception);

            OnFailed(folder);
        }
    }

    private ImportProgressCallbacks GetProgressCallbacks(PhotoImportFolderState photoImportFolder)
    {
        return new ImportProgressCallbacks
        {
            OnProgressChanged = (numberOfImportedFiles, numberOfFilesToImport) => OnProgressChanged(photoImportFolder, numberOfImportedFiles, numberOfFilesToImport),
            OnAlbumSelected = folderCurrentPosition => OnAlbumSelected(photoImportFolder, folderCurrentPosition),
            OnPhotoFileActivityChanged = OnPhotoFileActivityChanged,
        };
    }

    private void OnStarted(PhotoImportFolderState photoImportFolder)
    {
        photoImportFolder.Status = PhotoImportFolderStatus.Importing;

        SaveAndNotify(photoImportFolder);
    }

    private void OnProgressChanged(PhotoImportFolderState photoImportFolder, int numberOfImportedFiles, int numberOfFilesToImport)
    {
        photoImportFolder.NumberOfImportedFiles = numberOfImportedFiles;
        photoImportFolder.NumberOfFilesToImport = numberOfFilesToImport;

        SaveAndNotify(photoImportFolder);
    }

    private void OnAlbumSelected(PhotoImportFolderState photoImportFolder, PhotoImportFolderCurrentPosition folderCurrentPosition)
    {
        photoImportFolder.CurrentPosition = folderCurrentPosition;

        SaveAndNotify(photoImportFolder);
    }

    private void OnInterrupted(PhotoImportFolderState photoImportFolder)
    {
        photoImportFolder.Status = PhotoImportFolderStatus.Interrupted;

        SaveAndNotify(photoImportFolder);
    }

    private void OnSucceeded(PhotoImportFolderState photoImportFolder)
    {
        photoImportFolder.Status = PhotoImportFolderStatus.Succeeded;

        SaveAndNotify(photoImportFolder);
    }

    private void OnFailed(PhotoImportFolderState photoImportFolder)
    {
        photoImportFolder.Status = PhotoImportFolderStatus.Failed;

        SaveAndNotify(photoImportFolder);
    }

    private void SaveAndNotify(PhotoImportFolderState folder)
    {
        _ = _photoFolderService.UpdateFolderAsync(folder, CancellationToken.None);
    }

    private void OnPhotoFileActivityChanged(string filePath, Exception? exception = null)
    {
        var item = CreateActivityItem(filePath, exception);

        foreach (var listener in _photoImportActivityAware.Value)
        {
            listener.OnPhotoImportActivityChanged(item);
        }
    }

    private SyncActivityItem<long> CreateActivityItem(string filePath, Exception? exception)
    {
        return new SyncActivityItem<long>
        {
            Id = _stringIdMapper.GetId(filePath),
            NodeType = NodeType.File,
            Progress = Progress.Completed,
            Status = exception is null ? SyncActivityItemStatus.Succeeded : SyncActivityItemStatus.Failed,
            ActivityType = SyncActivityType.Upload,
            Stage = SyncActivityStage.Execution,
            ErrorCode = GetErrorCodeFromException(exception),
        };
    }

    private static FileSystemErrorCode GetErrorCodeFromException(Exception? exception)
    {
        return exception switch
        {
            null => FileSystemErrorCode.Unknown,
            IFileSystemErrorCodeProvider fileSystemClientException => fileSystemClientException.ErrorCode,
            _ => exception.IsFileAccessException() ? FileSystemErrorCode.UnauthorizedAccess : FileSystemErrorCode.Unknown,
        };
    }

    private sealed class StringIdMapper
    {
        private readonly ConcurrentDictionary<string, long> _map = new();
        private long _nextId;

        public long GetId(string value)
        {
            return _map.GetOrAdd(value, _ => Interlocked.Increment(ref _nextId));
        }
    }
}
