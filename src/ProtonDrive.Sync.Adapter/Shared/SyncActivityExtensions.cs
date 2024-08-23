using System;
using System.Diagnostics.CodeAnalysis;
using ProtonDrive.Shared;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.SyncActivity;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.Shared;

public static class SyncActivityExtensions
{
    public static SyncActivityItem<TId> GetSyncActivityItem<TId, TAltId>(this IIdentifiable<TId> node, NodeInfo<TAltId> nodeInfo)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return new SyncActivityItem<TId>
        {
            Source = SyncActivitySource.UpdateDetection,
            Id = node.Id,
            ActivityType = SyncActivityType.FetchUpdates,
            NodeType = nodeInfo.IsDirectory() ? NodeType.Directory : NodeType.File,
            Name = nodeInfo.Name,
            RelativeParentFolderPath = nodeInfo.GetParentFolderPath() ?? string.Empty,
            LocalRootPath = nodeInfo.Root?.LocalPath ?? string.Empty,
            Size = nodeInfo.IsDirectory() ? null : (nodeInfo.Size >= 0 ? nodeInfo.Size : null),
        };
    }

    public static SyncActivityItem<TId> GetSyncActivityItem<TId>(this ExecutableOperation<TId> operation)
        where TId : IEquatable<TId>
    {
        return operation.Model.GetSyncActivityItem(operation.GetActivityType());
    }

    public static SyncActivityItem<TId> GetSyncActivityItem<TId>(this IIdentifiable<TId> node, SyncActivityType activityType)
        where TId : IEquatable<TId>
    {
        return new SyncActivityItem<TId>
        {
            Source = activityType is SyncActivityType.FetchUpdates ? SyncActivitySource.UpdateDetection : SyncActivitySource.OperationExecution,
            Id = node.Id,
            ActivityType = activityType,
        };
    }

    public static SyncActivityItem<TId> GetSyncActivityItem<TId, TAltId>(
            this ExecutableOperation<TId> operation,
            NodeInfo<TAltId> nodeInfo,
            NodeInfo<TAltId>? destinationInfo,
            IRevision? sourceRevision = null)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return new SyncActivityItem<TId>
        {
            Source = SyncActivitySource.OperationExecution,
            Id = operation.Model.Id,
            NodeType = nodeInfo.IsDirectory() ? NodeType.Directory : NodeType.File,
            ActivityType = operation.GetActivityType(nodeInfo, destinationInfo),
            Name = string.IsNullOrEmpty(destinationInfo?.Name) ? nodeInfo.Name : destinationInfo.Name,
            RelativeParentFolderPath = destinationInfo?.GetParentFolderPath() ?? nodeInfo.GetParentFolderPath() ?? string.Empty,
            LocalRootPath = destinationInfo?.Root?.LocalPath ?? nodeInfo.Root?.LocalPath ?? string.Empty,
            Size = nodeInfo.IsDirectory() ? null : (sourceRevision?.Size ?? (nodeInfo.Size >= 0 ? nodeInfo.Size : null)),
        };
    }

    public static SyncActivityItem<TId> WithReplica<TId>(this SyncActivityItem<TId> syncActivity, Replica replica)
    {
        return syncActivity with
        {
            Replica = replica,
            ActivityType = syncActivity.NodeType is NodeType.File && syncActivity.ActivityType is SyncActivityType.Create
                ? replica is Replica.Local ? SyncActivityType.Download : SyncActivityType.Upload
                : syncActivity.ActivityType,
        };
    }

    public static (FileSystemErrorCode ErrorCode, string? ErrorMessage) GetErrorInfo(this Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            return (FileSystemErrorCode.Cancelled, null);
        }

        Exception? ex = exception;
        string? errorMessage;
        do
        {
            if (ex is FileSystemClientException clientException)
            {
                if (clientException.IsInnerExceptionMessageAuthoritative)
                {
                    errorMessage = clientException.InnerException?.Message;
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        return (clientException.ErrorCode, errorMessage);
                    }
                }
                else
                {
                    return (clientException.ErrorCode, null);
                }
            }

            errorMessage = ex.Message;
            ex = ex.InnerException;
        }
        while (ex != null);

        return (FileSystemErrorCode.Unknown, string.IsNullOrEmpty(errorMessage) ? null : errorMessage);
    }

    private static SyncActivityType GetActivityType<TId>(this ExecutableOperation<TId> operation)
        where TId : IEquatable<TId>
    {
        return operation.Type switch
        {
            /* File activity type Create is later converted by the SyncAgent into Upload or Download */
            OperationType.Create => SyncActivityType.Create,
            OperationType.Edit => SyncActivityType.Create,
            /* Without additional data, we do not know for sure whether it's Move or Rename */
            OperationType.Move => SyncActivityType.Move,
            OperationType.Delete => SyncActivityType.Delete,
            _ => throw new InvalidOperationException($"Operation type value {operation.Type} is not supported"),
        };
    }

    private static SyncActivityType GetActivityType<TId, TAltId>(
        this ExecutableOperation<TId> operation,
        NodeInfo<TAltId> nodeInfo,
        NodeInfo<TAltId>? destinationInfo)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return operation.Type switch
        {
            /* File activity type Create is later converted by the SyncAgent into Upload or Download */
            OperationType.Create => SyncActivityType.Create,
            OperationType.Edit => SyncActivityType.Create,
            OperationType.Move => operation.IsMove(nodeInfo, destinationInfo) ? SyncActivityType.Move : SyncActivityType.Rename,
            OperationType.Delete => SyncActivityType.Delete,
            _ => throw new InvalidOperationException($"Operation type value {operation.Type} is not supported"),
        };
    }

    private static bool IsMove<TId, TAltId>(this ExecutableOperation<TId> operation, NodeInfo<TAltId> nodeInfo, NodeInfo<TAltId>? destinationInfo)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return operation.Type is OperationType.Move &&
               destinationInfo is not null &&
               ((!IsNullOrDefault(destinationInfo.ParentId) &&
                 !IsNullOrDefault(nodeInfo.ParentId) &&
                 !destinationInfo.ParentId.Equals(nodeInfo.ParentId)) ||
                !string.IsNullOrEmpty(destinationInfo.Path));
    }

    private static bool IsNullOrDefault<T>([NotNullWhen(false)] T? value)
    {
        return value is null || value.Equals(default);
    }
}
