using System;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.Shared;

internal interface IFileSystemAccessRateLimiter<TId>
    where TId : IEquatable<TId>
{
    bool CanExecute(ExecutableOperation<TId> operation, out ExecutionResultCode resultCode);
    void HandleSuccess(ExecutableOperation<TId> operation);
    void HandleFailure(ExecutableOperation<TId> operation, ExecutionResultCode resultCode, FileSystemErrorCode errorCode);
    void Reset();
}
