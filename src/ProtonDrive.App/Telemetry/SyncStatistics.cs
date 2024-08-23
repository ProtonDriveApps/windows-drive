using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Settings;
using ProtonDrive.App.Sync;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.SyncActivity;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.App.Telemetry;

public sealed class SyncStatistics : ISyncStateAware, ISyncActivityAware, IMappingsAware
{
    private readonly SyncedItemCounters _syncedItemCounters;
    private readonly SyncedItemCounters _syncedSharedWithMeItemCounters;
    private readonly ConcurrentDictionary<FileSystemErrorCode, int> _numberOfFailuresByErrorCode = new();

    private int _numberOfSyncPasses;
    private int _numberOfUnhandledExceptionsDuringSync;

    private int _numberOfSuccessfulFileOperations;
    private int _numberOfSuccessfulFolderOperations;
    private int _numberOfFailedFileOperations;
    private int _numberOfFailedFolderOperations;

    private string? _sharedWithMeRootFolderPath;

    public SyncStatistics(SyncedItemCounters syncedItemCounters, SyncedItemCounters syncedSharedWithMeItemCounters)
    {
        _syncedItemCounters = syncedItemCounters;
        _syncedSharedWithMeItemCounters = syncedSharedWithMeItemCounters;
    }

    public int NumberOfSyncPasses => _numberOfSyncPasses;
    public int NumberOfUnhandledExceptionsDuringSync => _numberOfUnhandledExceptionsDuringSync;

    public int NumberOfSuccessfulFileOperations => _numberOfSuccessfulFileOperations;
    public int NumberOfSuccessfulFolderOperations => _numberOfSuccessfulFolderOperations;
    public int NumberOfFailedFileOperations => _numberOfFailedFileOperations;
    public int NumberOfFailedFolderOperations => _numberOfFailedFolderOperations;

    public DocumentNameMigrationStatistics DocumentNameMigration { get; } = new();

    void ISyncStateAware.OnSyncStateChanged(SyncState value)
    {
        if (value.Status is SyncStatus.DetectingUpdates)
        {
            Interlocked.Increment(ref _numberOfSyncPasses);
        }
        else if (value.Status is SyncStatus.Failed)
        {
            Interlocked.Increment(ref _numberOfUnhandledExceptionsDuringSync);
        }
    }

    void ISyncActivityAware.OnSyncActivityChanged(SyncActivityItem<long> item)
    {
        if (item.ActivityType is SyncActivityType.FetchUpdates
            || item.ErrorCode is FileSystemErrorCode.Cancelled)
        {
            return;
        }

        switch (item.Status)
        {
            case SyncActivityItemStatus.Skipped when item.ErrorCode is FileSystemErrorCode.LastWriteTimeTooRecent:
                _numberOfFailuresByErrorCode.AddOrUpdate(
                    item.ErrorCode,
                    addValueFactory: _ => 1,
                    updateValueFactory: (_, numberOfFailures) => numberOfFailures + 1);
                break;

            case SyncActivityItemStatus.Cancelled when item.ErrorCode is FileSystemErrorCode.TransferAbortedDueToFileChange:
                _numberOfFailuresByErrorCode.AddOrUpdate(
                    item.ErrorCode,
                    addValueFactory: _ => 1,
                    updateValueFactory: (_, numberOfFailures) => numberOfFailures + 1);
                break;

            case SyncActivityItemStatus.Failed:

                _syncedItemCounters.IncrementFailures(item.Id);

                if (IsSharedWithMeItem(item.LocalRootPath))
                {
                    _syncedSharedWithMeItemCounters.IncrementFailures(item.Id);
                }

                switch (item.NodeType)
                {
                    case NodeType.File:
                        Interlocked.Increment(ref _numberOfFailedFileOperations);
                        break;

                    case NodeType.Directory:
                        Interlocked.Increment(ref _numberOfFailedFolderOperations);
                        break;
                }

                switch (item.ErrorCode)
                {
                    case FileSystemErrorCode.DuplicateName:
                    case FileSystemErrorCode.InvalidName:
                    case FileSystemErrorCode.SharingViolation:
                    case FileSystemErrorCode.UnauthorizedAccess:
                    case FileSystemErrorCode.FreeSpaceExceeded:
                    case FileSystemErrorCode.TooManyChildren:
                    case FileSystemErrorCode.Partial:
                    case FileSystemErrorCode.DirectoryNotFound:
                    case FileSystemErrorCode.ObjectNotFound:
                    case FileSystemErrorCode.PathNotFound:
                    case FileSystemErrorCode.Unknown:
                    case FileSystemErrorCode.IntegrityFailure:
                    case FileSystemErrorCode.MetadataMismatch:
                        _numberOfFailuresByErrorCode.AddOrUpdate(
                            item.ErrorCode,
                            addValueFactory: _ => 1,
                            updateValueFactory: (_, numberOfFailures) => numberOfFailures + 1);
                        break;
                }

                break;

            case SyncActivityItemStatus.Succeeded:

                _syncedItemCounters.IncrementSuccesses(item.Id);

                if (IsSharedWithMeItem(item.LocalRootPath))
                {
                    _syncedSharedWithMeItemCounters.IncrementSuccesses(item.Id);
                }

                switch (item.NodeType)
                {
                    case NodeType.File:
                        Interlocked.Increment(ref _numberOfSuccessfulFileOperations);
                        break;
                    case NodeType.Directory:
                        Interlocked.Increment(ref _numberOfSuccessfulFolderOperations);
                        break;
                }

                break;
        }
    }

    public int GetNumberOfFailuresByErrorCode(FileSystemErrorCode errorCode)
    {
        _numberOfFailuresByErrorCode.TryGetValue(errorCode, out var result);
        return result;
    }

    public (int Successes, int Failures) GetUniqueSyncedFileCounters()
    {
        return _syncedItemCounters.GetCounters();
    }

    public (int Successes, int Failures) GetUniqueSyncedSharedWithMeItemCounters()
    {
        return _syncedSharedWithMeItemCounters.GetCounters();
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _numberOfSyncPasses, 0);
        Interlocked.Exchange(ref _numberOfUnhandledExceptionsDuringSync, 0);

        Interlocked.Exchange(ref _numberOfSuccessfulFileOperations, 0);
        Interlocked.Exchange(ref _numberOfSuccessfulFolderOperations, 0);
        Interlocked.Exchange(ref _numberOfFailedFileOperations, 0);
        Interlocked.Exchange(ref _numberOfFailedFolderOperations, 0);

        _numberOfFailuresByErrorCode.Clear();
        _syncedItemCounters.Reset();
    }

    public void OnMappingsChanged(IReadOnlyCollection<RemoteToLocalMapping> activeMappings, IReadOnlyCollection<RemoteToLocalMapping> deletedMappings)
    {
        _sharedWithMeRootFolderPath = activeMappings
            .Where(x => x.Type is MappingType.SharedWithMeRootFolder)
            .Select(x => x.Local.RootFolderPath)
            .FirstOrDefault();
    }

    private bool IsSharedWithMeItem(string localPath)
    {
        return _sharedWithMeRootFolderPath is not null && localPath.StartsWith(_sharedWithMeRootFolderPath);
    }
}
