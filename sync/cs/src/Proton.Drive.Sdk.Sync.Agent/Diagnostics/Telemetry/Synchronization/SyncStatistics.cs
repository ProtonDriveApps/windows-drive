using System.Collections.Concurrent;
using Proton.Drive.Sdk.Sync.Agent.Mapping;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Diagnostics.TemporaryFiles;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.SyncActivity;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry.Synchronization;

public sealed class SyncStatistics : ISyncStateAware, ISyncActivityAware, IMappingsAware
{
    private readonly SyncedItemCounters _syncedItemCounters;
    private readonly SyncedItemCounters _syncedSharedWithMeItemCounters;
    private readonly IExcelTemporaryFileDetectionCountProvider _excelTemporaryFileDetectionCountProvider;
    private readonly ConcurrentDictionary<FileSystemErrorCode, int> _numberOfFailuresByErrorCode = new();

    private int _numberOfSyncPasses;
    private int _numberOfUnhandledExceptionsDuringSync;

    private int _numberOfSuccessfulFileOperations;
    private int _numberOfSuccessfulExcelTemporaryFileCandidateOperations;
    private int _numberOfSuccessfulFolderOperations;
    private int _numberOfFailedFileOperations;
    private int _numberOfFailedFolderOperations;

    private string? _sharedWithMeRootFolderPath;

    public SyncStatistics(
        SyncedItemCounters syncedItemCounters,
        SyncedItemCounters syncedSharedWithMeItemCounters,
        IExcelTemporaryFileDetectionCountProvider excelTemporaryFileDetectionCountProvider)
    {
        _syncedItemCounters = syncedItemCounters;
        _syncedSharedWithMeItemCounters = syncedSharedWithMeItemCounters;
        _excelTemporaryFileDetectionCountProvider = excelTemporaryFileDetectionCountProvider;
    }

    public int NumberOfSyncPasses => _numberOfSyncPasses;
    public int NumberOfUnhandledExceptionsDuringSync => _numberOfUnhandledExceptionsDuringSync;

    public int NumberOfSuccessfulFileOperations => _numberOfSuccessfulFileOperations;
    public int NumberOfSuccessfulFolderOperations => _numberOfSuccessfulFolderOperations;
    public int NumberOfFailedFileOperations => _numberOfFailedFileOperations;
    public int NumberOfFailedFolderOperations => _numberOfFailedFolderOperations;

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
                    case FileSystemErrorCode.CloudFileProviderNotRunning:
                    case FileSystemErrorCode.CyclicRedundancyCheck:
                    case FileSystemErrorCode.LocalStorageError:
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
                        if (MicrosoftExcelTemporaryFile.IsCandidate(item.Name))
                        {
                            Interlocked.Increment(ref _numberOfSuccessfulExcelTemporaryFileCandidateOperations);
                        }

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

    public (int NumberOfSuccessfullySyncedExcelTemporaryFiles, int NumberOfDetectedExcelTemporaryFiles) GetExcelTemporaryFileCandidateCounters()
    {
        return (_numberOfSuccessfulExcelTemporaryFileCandidateOperations, _excelTemporaryFileDetectionCountProvider.GetCount());
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

        Interlocked.Exchange(ref _numberOfSuccessfulExcelTemporaryFileCandidateOperations, 0);
        _excelTemporaryFileDetectionCountProvider.Reset();
    }

    void IMappingsAware.OnMappingsChanged(IReadOnlyCollection<RemoteToLocalMapping> activeMappings, IReadOnlyCollection<RemoteToLocalMapping> deletedMappings)
    {
        _sharedWithMeRootFolderPath = activeMappings
            .Where(x => x.Type is MappingType.SharedWithMeRootFolder)
            .Select(x => x.Local.Path)
            .FirstOrDefault();
    }

    private bool IsSharedWithMeItem(string localPath)
    {
        return _sharedWithMeRootFolderPath is not null && localPath.StartsWith(_sharedWithMeRootFolderPath);
    }
}
