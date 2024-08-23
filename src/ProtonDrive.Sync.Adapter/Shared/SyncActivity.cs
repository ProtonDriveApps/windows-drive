using System;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.Sync.Adapter.Shared;

internal sealed class SyncActivity<TId>
    where TId : IEquatable<TId>
{
    public event EventHandler<SyncActivityChangedEventArgs<TId>>? SyncActivityChanged;

    public void OnChanged(SyncActivityItem<TId> item, SyncActivityItemStatus status, FileSystemErrorCode? exErrorCode = FileSystemErrorCode.Unknown)
    {
        item = item with
        {
            Status = status,
            ErrorCode = exErrorCode ?? FileSystemErrorCode.Unknown,
        };

        SyncActivityChanged?.Invoke(this, new SyncActivityChangedEventArgs<TId>(item));
    }

    public void OnSucceeded(SyncActivityItem<TId> item)
    {
        item = item with
        {
            Status = SyncActivityItemStatus.Succeeded,
            Progress = Progress.Completed,
        };

        SyncActivityChanged?.Invoke(this, new SyncActivityChangedEventArgs<TId>(item));
    }

    public void OnProgress(SyncActivityItem<TId> item, Progress progress)
    {
        item = item with
        {
            Status = SyncActivityItemStatus.InProgress,
            Progress = progress,
        };

        SyncActivityChanged?.Invoke(this, new SyncActivityChangedEventArgs<TId>(item));
    }

    public void OnCancelled(SyncActivityItem<TId> item, FileSystemErrorCode errorCode)
    {
        item = item with
        {
            Status = SyncActivityItemStatus.Cancelled,
            ErrorCode = errorCode,
        };

        SyncActivityChanged?.Invoke(this, new SyncActivityChangedEventArgs<TId>(item));
    }

    public void OnWarning(SyncActivityItem<TId> item, FileSystemErrorCode errorCode, string? errorMessage = default)
    {
        item = item with
        {
            Status = SyncActivityItemStatus.Warning,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
        };

        SyncActivityChanged?.Invoke(this, new SyncActivityChangedEventArgs<TId>(item));
    }

    public void OnFailed(SyncActivityItem<TId> item, FileSystemErrorCode errorCode, string? errorMessage = default)
    {
        item = item with
        {
            Status = SyncActivityItemStatus.Failed,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
        };

        SyncActivityChanged?.Invoke(this, new SyncActivityChangedEventArgs<TId>(item));
    }
}
