using Proton.Drive.Sdk.Sync.Shared.Adapters;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.Operations;

namespace Proton.Drive.Sdk.Sync.Adapter.Shared;

internal interface IFileSystemAccessRateLimiter<TId>
    where TId : IEquatable<TId>
{
    bool CanExecute(ExecutableOperation<TId> operation, out ExecutionResultCode resultCode);
    void HandleSuccess(ExecutableOperation<TId> operation);
    void HandleFailure(ExecutableOperation<TId> operation, ExecutionResultCode resultCode, FileSystemErrorCode errorCode);
    void Reset();
}
