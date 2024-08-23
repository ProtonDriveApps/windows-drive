using System;
using ProtonDrive.Shared;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.Shared;

internal sealed class FileSystemAccessRateLimiter<TId> : IFileSystemAccessRateLimiter<TId>
    where TId : IEquatable<TId>
{
    private readonly TimeSpan _minDelay = TimeSpan.FromSeconds(5);
    private readonly IRateLimiter<TId> _itemRetryRateLimiter;
    private readonly IRateLimiter<TId> _parentRetryRateLimiter;
    private readonly IRateLimiter<TId> _fileRevisionUploadRateLimiter;

    public FileSystemAccessRateLimiter(IClock clock, TimeSpan maxRetryDelay, TimeSpan maxRevisionCreationDelay)
    {
        _itemRetryRateLimiter = new RateLimiter<TId>(clock, _minDelay, maxRetryDelay);
        _parentRetryRateLimiter = new RateLimiter<TId>(clock, _minDelay, maxRetryDelay);
        _fileRevisionUploadRateLimiter = maxRevisionCreationDelay != default
            ? new RateLimiterWithRecovery<TId>(clock, _minDelay, maxRevisionCreationDelay)
            : new NullRateLimiter<TId>();
    }

    public bool CanExecute(ExecutableOperation<TId> operation, out ExecutionResultCode resultCode)
    {
        if (!_itemRetryRateLimiter.CanExecute(operation.Model.Id))
        {
            resultCode = ExecutionResultCode.RetryRateLimitExceeded;
            return false;
        }

        if (AffectsParent(operation) && !_parentRetryRateLimiter.CanExecute(operation.Model.ParentId))
        {
            resultCode = ExecutionResultCode.RetryRateLimitExceeded;
            return false;
        }

        if (IsFileRevisionUpload(operation) && !_fileRevisionUploadRateLimiter.CanExecute(operation.Model.Id))
        {
            resultCode = ExecutionResultCode.AccessRateLimitExceeded;
            return false;
        }

        resultCode = ExecutionResultCode.Success;
        return true;
    }

    public void HandleSuccess(ExecutableOperation<TId> operation)
    {
        _itemRetryRateLimiter.ResetRate(operation.Model.Id);

        if (AffectsParent(operation))
        {
            _parentRetryRateLimiter.ResetRate(operation.Model.ParentId);
        }

        if (IsFileRevisionUpload(operation))
        {
            _fileRevisionUploadRateLimiter.DecreaseRate(operation.Model.Id);
        }
    }

    public void HandleFailure(ExecutableOperation<TId> operation, ExecutionResultCode resultCode, FileSystemErrorCode errorCode)
    {
        if (resultCode is not
            (ExecutionResultCode.Error
            or ExecutionResultCode.NameConflict
            or ExecutionResultCode.DirtyNode
            or ExecutionResultCode.DirtyBranch
            or ExecutionResultCode.DirtyDestination))
        {
            return;
        }

        _itemRetryRateLimiter.DecreaseRate(operation.Model.Id);

        if (AffectsParent(operation, errorCode))
        {
            _parentRetryRateLimiter.DecreaseRate(operation.Model.ParentId);
        }
    }

    public void Reset()
    {
        _itemRetryRateLimiter.Reset();
        _parentRetryRateLimiter.Reset();
        _fileRevisionUploadRateLimiter.Reset();
    }

    private static bool AffectsParent(ExecutableOperation<TId> operation, FileSystemErrorCode errorCode)
    {
        return AffectsParent(operation) && errorCode is FileSystemErrorCode.TooManyChildren;
    }

    private static bool AffectsParent(ExecutableOperation<TId> operation)
    {
        return operation.Type is OperationType.Create or OperationType.Move;
    }

    private static bool IsFileRevisionUpload(ExecutableOperation<TId> operation)
    {
        return operation.Model.Type is NodeType.File && operation.Type is OperationType.Edit;
    }
}
