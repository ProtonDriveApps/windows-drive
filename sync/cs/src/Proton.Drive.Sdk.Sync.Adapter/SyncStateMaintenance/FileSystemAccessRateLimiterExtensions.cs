using Proton.Drive.Sdk.Sync.Adapter.Shared;
using Proton.Drive.Sdk.Sync.Shared.Adapters;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.Operations;

namespace Proton.Drive.Sdk.Sync.Adapter.SyncStateMaintenance;

internal static class FileSystemAccessRateLimiterExtensions
{
    public static bool CanExecuteHydration<TId>(
        this IFileSystemAccessRateLimiter<TId> limiter,
        FileSystemNodeModel<TId> nodeModel,
        out ExecutionResultCode resultCode)
        where TId : IEquatable<TId>
    {
        var operation = GetFakeOperation<TId, TId>(nodeModel);

        return limiter.CanExecute(operation, out resultCode);
    }

    public static void HandleHydrationSuccess<TId>(
        this IFileSystemAccessRateLimiter<TId> limiter,
        FileSystemNodeModel<TId> nodeModel)
        where TId : IEquatable<TId>
    {
        var operation = GetFakeOperation<TId, TId>(nodeModel);

        limiter.HandleSuccess(operation);
    }

    public static void HandleHydrationFailure<TId>(
        this IFileSystemAccessRateLimiter<TId> limiter,
        FileSystemNodeModel<TId> nodeModel)
        where TId : IEquatable<TId>
    {
        var operation = GetFakeOperation<TId, TId>(nodeModel);

        limiter.HandleFailure(operation, ExecutionResultCode.Error, FileSystemErrorCode.Unknown);
    }

    private static ExecutableOperation<TId> GetFakeOperation<TId, TAltId>(FileSystemNodeModel<TId> nodeModel)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        var operationNodeModel = new AltIdentifiableFileSystemNodeModel<TId, TId>
        {
            Type = NodeType.File,
            Id = nodeModel.Id,
            AltId = nodeModel.Id,
        };

        return new ExecutableOperation<TId>(OperationType.Edit, operationNodeModel, backup: false);
    }
}
