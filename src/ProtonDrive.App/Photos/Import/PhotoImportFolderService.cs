using Microsoft.Extensions.Logging;
using ProtonDrive.App.Account;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Mapping.SyncFolders;
using ProtonDrive.App.Services;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Photos.Import;

internal sealed class PhotoImportFolderService : IPhotoImportFolderService, IStartableService, IStoppableService, IAccountSwitchingAware
{
    private readonly ISyncFolderService _syncFolderService;
    private readonly IRepository<PhotoImportSettings> _settingsRepository;
    private readonly Lazy<IEnumerable<IPhotoImportFoldersAware>> _folderObservers;
    private readonly ILogger<PhotoImportFolderService> _logger;

    private readonly List<PhotoImportFolderState> _folders = [];
    private readonly IScheduler _scheduler = new SerialScheduler();

    private int _latestId;
    private volatile bool _stopping;
    private volatile IReadOnlyCollection<PhotoImportFolderState> _foldersCollection = [];

    public PhotoImportFolderService(
        ISyncFolderService syncFolderService,
        IRepository<PhotoImportSettings> settingsRepository,
        Lazy<IEnumerable<IPhotoImportFoldersAware>> folderObservers,
        ILogger<PhotoImportFolderService> logger)
    {
        _syncFolderService = syncFolderService;
        _settingsRepository = settingsRepository;
        _folderObservers = folderObservers;
        _logger = logger;
    }

    Task IStartableService.StartAsync(CancellationToken cancellationToken)
    {
        return Schedule(LoadFolders, cancellationToken);
    }

    async Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug($"{nameof(PhotoImportFolderService)} is stopping");
        _stopping = true;

        await WaitForCompletionAsync().ConfigureAwait(false);

        _logger.LogInformation($"{nameof(PhotoImportFolderService)} stopped");
    }

    void IAccountSwitchingAware.OnAccountSwitched()
    {
        _ = Schedule(LoadFolders, CancellationToken.None);
    }

    public SyncFolderValidationResult ValidateFolder(string path)
    {
        return _syncFolderService.ValidateSyncFolder(path, otherPaths: _foldersCollection.Select(x => x.Path));
    }

    public Task AddFolderAsync(string path, CancellationToken cancellationToken)
    {
        return Schedule(InternalAddFolderAsync, cancellationToken);

        void InternalAddFolderAsync()
        {
            Ensure.NotNullOrEmpty(path, nameof(path));

            var pathToLog = _logger.GetSensitiveValueForLogging(path);
            _logger.LogInformation("Requested to add Photo Import folder \"{Path}\"", pathToLog);

            if (_folders.Any(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("Ignored Photo Import folder \"{Path}\", it is already added", pathToLog);
                return;
            }

            var folder = new PhotoImportFolderState(GetNextId(), path);
            _folders.Add(folder);
            SaveFolders();
            OnFolderChanged(SyncFolderChangeType.Added, folder);

            _logger.LogInformation("Added Photo Import folder \"{Path}\"", pathToLog);
        }
    }

    public Task UpdateFolderAsync(PhotoImportFolderState folder, CancellationToken cancellationToken)
    {
        return Schedule(InternalUpdateFolderAsync, cancellationToken);

        void InternalUpdateFolderAsync()
        {
            if (_folders.FirstOrDefault(x => x == folder) is null)
            {
                // The folder might already be removed when the import service attempts to update it
                return;
            }

            SaveFolders();
            OnFolderChanged(SyncFolderChangeType.Updated, folder);
        }
    }

    public Task RetryImportAsync(PhotoImportFolderState folder, CancellationToken cancellationToken)
    {
        return Schedule(InternalRetryImportAsync, cancellationToken);

        void InternalRetryImportAsync()
        {
            var pathToLog = _logger.GetSensitiveValueForLogging(folder.Path);
            _logger.LogInformation("Requested to retry Photo Import folder \"{Path}\"", pathToLog);

            if (_folders.FirstOrDefault(x => x == folder) is null)
            {
                _logger.LogWarning("Photo Import folder \"{Path}\" not found", pathToLog);
                return;
            }

            folder.Status = PhotoImportFolderStatus.NotStarted;
            SaveFolders();
            OnFolderChanged(SyncFolderChangeType.Updated, folder);

            _logger.LogInformation("Status of Photo Import folder \"{Path}\" reset to {Status}", pathToLog, PhotoImportFolderStatus.NotStarted);
        }
    }

    public Task RemoveFolderAsync(PhotoImportFolderState folder, CancellationToken cancellationToken)
    {
        return Schedule(InternalRemoveFolderAsync, cancellationToken);

        void InternalRemoveFolderAsync()
        {
            var pathToLog = _logger.GetSensitiveValueForLogging(folder.Path);
            _logger.LogInformation("Requested to remove Photo Import folder \"{Path}\"", pathToLog);

            if (!_folders.Remove(folder))
            {
                _logger.LogWarning("Photo Import folder \"{Path}\" not found", pathToLog);
                return;
            }

            SaveFolders();
            OnFolderChanged(SyncFolderChangeType.Removed, folder);

            _logger.LogInformation("Removed Photo Import folder \"{Path}\"", pathToLog);
        }
    }

    internal Task WaitForCompletionAsync()
    {
        // Wait for all scheduled tasks to complete
        return _scheduler.Schedule(() => false);
    }

    private int GetNextId() => ++_latestId;

    private void LoadFolders()
    {
        ClearFolders();

        var settings = _settingsRepository.Get() ?? new PhotoImportSettings(Folders: []);

        foreach (var folder in settings.Folders)
        {
            _folders.Add(folder);
            OnFolderChanged(SyncFolderChangeType.Added, folder);
        }

        _latestId = _folders.DefaultIfEmpty().Max(x => x?.Id ?? 0);
        _foldersCollection = [.. _folders];
    }

    private void SaveFolders()
    {
        _foldersCollection = [.. _folders];
        _settingsRepository.Set(new PhotoImportSettings(_foldersCollection));
    }

    private void ClearFolders()
    {
        foreach (var folder in _folders)
        {
            OnFolderChanged(SyncFolderChangeType.Removed, folder);
        }

        _folders.Clear();
        _latestId = 0;
    }

    private void OnFolderChanged(SyncFolderChangeType changeType, PhotoImportFolderState folder)
    {
        foreach (var observer in _folderObservers.Value)
        {
            observer.OnPhotoImportFolderChanged(changeType, folder);
        }
    }

    private async Task Schedule(Action action, CancellationToken cancellationToken)
    {
        if (_stopping)
        {
            return;
        }

        using (await _scheduler.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_stopping)
            {
                return;
            }

            action.Invoke();
        }
    }
}
