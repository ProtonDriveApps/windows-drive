using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Account;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Mapping.SyncFolders;
using ProtonDrive.App.Services;
using ProtonDrive.App.Settings;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.FileSystem.Photos;
using ProtonDrive.Sync.Shared.SyncActivity;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.App.Photos.Import;

internal sealed class PhotoImportService : IStartableService, IStoppableService, IAccountSwitchingAware, IMappingsSetupStateAware
{
    private readonly Lazy<IEnumerable<IPhotoImportFoldersAware>> _photoImportFoldersAware;
    private readonly Lazy<IEnumerable<IPhotoImportActivityAware>> _photoImportActivityAware;
    private readonly IPhotoFolderService _photoFolderService;
    private readonly IPhotoImportEngineFactory _photoImportEngineFactory;
    private readonly ILogger<PhotoImportService> _logger;

    private readonly CoalescingAction _photoImport;
    private readonly SemaphoreSlim _currentMappingSemaphore = new(1);
    private readonly StringIdMapper _stringIdMapper = new();

    private volatile bool _stopping;
    private PhotoImportSettings _settings = new([]);
    private MappingsSetupState _mappingsSetupState = MappingsSetupState.None;
    private RemoteToLocalMapping? _currentMapping;

    public PhotoImportService(
        Lazy<IEnumerable<IPhotoImportFoldersAware>> photoImportFoldersAware,
        Lazy<IEnumerable<IPhotoImportActivityAware>> photoImportActivityAware,
        IPhotoFolderService photoFolderService,
        IPhotoImportEngineFactory photoImportEngineFactory,
        ILogger<PhotoImportService> logger)
    {
        _photoImportFoldersAware = photoImportFoldersAware;
        _photoImportActivityAware = photoImportActivityAware;
        _photoFolderService = photoFolderService;
        _photoImportEngineFactory = photoImportEngineFactory;
        _logger = logger;

        _photoImport = _logger.GetCoalescingActionWithExceptionsLoggingAndCancellationHandling(ImportAsync, nameof(PhotoImportService));
    }

    Task IStartableService.StartAsync(CancellationToken cancellationToken)
    {
        LoadSettings();

        return Task.CompletedTask;
    }

    void IAccountSwitchingAware.OnAccountSwitched()
    {
        LoadSettings();
    }

    void IMappingsSetupStateAware.OnMappingsSetupStateChanged(MappingsSetupState value)
    {
        if (_stopping)
        {
            return;
        }

        if (value.Status is MappingSetupStatus.SettingUp)
        {
            return;
        }

        _mappingsSetupState = value;

        if (value.Status is MappingSetupStatus.Succeeded or MappingSetupStatus.PartiallySucceeded)
        {
            _photoImport.Run();
        }
        else
        {
            _photoImport.Cancel();
        }
    }

    async Task IMappingsSetupStateAware.OnMappingsSettingUpAsync()
    {
        using (await _currentMappingSemaphore.LockAsync(CancellationToken.None).ConfigureAwait(false))
        {
            var mapping = _currentMapping;

            // If currently being imported mapping has been deleted or is not set up, we cancel Photo import
            if (mapping is not null && (mapping.Status is not MappingStatus.Complete || !mapping.HasSetupSucceeded))
            {
                _photoImport.Cancel();
            }
        }
    }

    async Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{nameof(PhotoImportService)} is stopping");
        _stopping = true;
        _photoImport.Cancel();

        await WaitForCompletionAsync().ConfigureAwait(false);

        _logger.LogInformation($"{nameof(PhotoImportService)} stopped");
    }

    internal Task WaitForCompletionAsync()
    {
        return _photoImport.WaitForCompletionAsync();
    }

    private async Task ImportAsync(CancellationToken cancellationToken)
    {
        var mappingsSetupState = _mappingsSetupState;

        if (mappingsSetupState.Status is not MappingSetupStatus.Succeeded and not MappingSetupStatus.PartiallySucceeded)
        {
            _logger.LogInformation("Photo import skipped, mapping setup state is {MappingSetupStatus}", mappingsSetupState.Status);
            return;
        }

        var mappings = mappingsSetupState.Mappings
            .Where(x => x.Type is MappingType.PhotoImport)
            .ToList();

        RemoveUntrackedImportFolders([.. mappings.Select(m => m.Id)]);

        foreach (var mapping in mappings)
        {
            // Importing one folder at a time
            await ImportFolderAsync(mapping, cancellationToken).ConfigureAwait(false);
        }
    }

    private void RemoveUntrackedImportFolders(HashSet<int> trackedMappingIds)
    {
        var untrackedFolderMappingIds = _settings.Folders.Select(x => x.MappingId).Where(x => !trackedMappingIds.Contains(x)).ToList();
        var savingRequired = false;

        foreach (var untrackedMappingId in untrackedFolderMappingIds)
        {
            var folderToRemove = _settings.Folders.FirstOrDefault(x => x.MappingId == untrackedMappingId);
            if (folderToRemove is null)
            {
                continue;
            }

            _settings.Folders.Remove(folderToRemove);
            OnPhotoImportFolderRemoved(folderToRemove);
            savingRequired = true;
        }

        if (savingRequired)
        {
            SaveSettings();
        }
    }

    private async Task ImportFolderAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.Status is not MappingStatus.Complete || !mapping.HasSetupSucceeded)
        {
            return;
        }

        var photoImportFolder = _settings.Folders.FirstOrDefault(x => x.MappingId == mapping.Id);

        if (photoImportFolder is null)
        {
            photoImportFolder = new PhotoImportFolderState(mapping.Id, mapping.Local.Path);
            _settings.Folders.Add(photoImportFolder);
            OnPhotoImportFolderAdded(photoImportFolder);
        }

        if (photoImportFolder.Status
            is not PhotoImportFolderStatus.NotStarted
            and not PhotoImportFolderStatus.Importing
            and not PhotoImportFolderStatus.Interrupted)
        {
            return;
        }

        using (await _currentMappingSemaphore.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            _currentMapping = mapping;
        }

        try
        {
            await ImportFolderAsync(mapping, photoImportFolder, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _currentMapping = null;
        }
    }

    private async Task ImportFolderAsync(RemoteToLocalMapping mapping, PhotoImportFolderState photoImportFolder, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Photo import started: mapping {MappingID}, folder \"{Path}\"",
                photoImportFolder.MappingId,
                _logger.GetSensitiveValueForLogging(mapping.Local.Path));

            OnStarted(photoImportFolder);

            var engine = _photoImportEngineFactory.CreateEngine(mapping, photoImportFolder.CurrentPosition);
            var callbacks = GetProgressCallbacks(photoImportFolder);

            await engine.ImportAsync(callbacks, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Photo import succeeded");
            OnSucceeded(photoImportFolder);
        }
        catch (OperationCanceledException)
        {
            // TODO: Avoid log-and-throw (antipattern)
            _logger.LogInformation("Photo import interrupted");
            OnInterrupted(photoImportFolder);
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

            photoImportFolder.ErrorCode = exception is PhotoImportException photoImportException
                ? photoImportException.ErrorCode
                : PhotoImportErrorCode.Unknown;

            OnFailed(photoImportFolder);
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

        OnPhotoImportFolderUpdated(photoImportFolder);
    }

    private void OnAlbumSelected(PhotoImportFolderState photoImportFolder, PhotoImportFolderCurrentPosition folderCurrentPosition)
    {
        photoImportFolder.CurrentPosition = folderCurrentPosition;

        SaveSettings();
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

    private void SaveAndNotify(PhotoImportFolderState photoImportFolder)
    {
        SaveSettings();
        OnPhotoImportFolderUpdated(photoImportFolder);
    }

    private void OnPhotoImportFolderAdded(PhotoImportFolderState folder)
    {
        OnPhotoImportFolderChanged(SyncFolderChangeType.Added, folder);
    }

    private void OnPhotoImportFolderUpdated(PhotoImportFolderState folder)
    {
        OnPhotoImportFolderChanged(SyncFolderChangeType.Updated, folder);
    }

    private void OnPhotoImportFolderRemoved(PhotoImportFolderState folder)
    {
        OnPhotoImportFolderChanged(SyncFolderChangeType.Removed, folder);
    }

    private void OnPhotoImportFolderChanged(SyncFolderChangeType changeType, PhotoImportFolderState folder)
    {
        foreach (var photoImportFoldersAware in _photoImportFoldersAware.Value)
        {
            photoImportFoldersAware.OnPhotoImportFolderChanged(changeType, folder);
        }
    }

    private void LoadSettings()
    {
        foreach (var folder in _settings.Folders)
        {
            OnPhotoImportFolderRemoved(folder);
        }

        _settings = _photoFolderService.GetSettings();

        foreach (var folder in _settings.Folders)
        {
            OnPhotoImportFolderAdded(folder);
        }
    }

    private void SaveSettings()
    {
        _photoFolderService.SetSettings(_settings);
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
