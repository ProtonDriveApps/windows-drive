using System;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared;
using ProtonDrive.Sync.Adapter.Shared;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Adapter.UpdateDetection.StateBased.Enumeration;
using ProtonDrive.Sync.Shared.ExecutionStatistics;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.Sync.Adapter.UpdateDetection.StateBased;

internal class DirtyNodeUpdateDetection<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly NodeEnumeration<TId, TAltId> _nodeEnumeration;
    private readonly ChildrenEnumeration<TId, TAltId> _childrenEnumeration;
    private readonly SyncActivity<TId> _syncActivity;
    private readonly ConcurrentExecutionStatistics _executionStatistics;

    public DirtyNodeUpdateDetection(
        NodeEnumeration<TId, TAltId> nodeEnumeration,
        ChildrenEnumeration<TId, TAltId> childrenEnumeration,
        SyncActivity<TId> syncActivity,
        ConcurrentExecutionStatistics executionStatistics)
    {
        _nodeEnumeration = nodeEnumeration;
        _childrenEnumeration = childrenEnumeration;
        _syncActivity = syncActivity;
        _executionStatistics = executionStatistics;
    }

    public async Task DetectUpdates(AdapterTreeNode<TId, TAltId> node, CancellationToken cancellationToken)
    {
        try
        {
            var result = await EnumerateNode(node, cancellationToken).ConfigureAwait(false);

            if (result.IsFailure)
            {
                HandleResult(result, node);

                return;
            }

            result = await EnumerateChildren(node, cancellationToken).ConfigureAwait(false);

            HandleResult(result, node);
        }
        catch (EscapeException)
        {
            // Unable to apply enumeration result due to Adapter Tree has diverged
            HandleEscape(node);
        }
    }

    private Task<Result<NodeInfo<TAltId>>> EnumerateNode(AdapterTreeNode<TId, TAltId> node, CancellationToken cancellationToken)
    {
        return _nodeEnumeration.ExecuteAsync(node, cancellationToken);
    }

    private Task<Result<NodeInfo<TAltId>>> EnumerateChildren(AdapterTreeNode<TId, TAltId> node, CancellationToken cancellationToken)
    {
        return _childrenEnumeration.ExecuteAsync(node, cancellationToken);
    }

    private void HandleResult(Result<NodeInfo<TAltId>> result, IIdentifiable<TId> node)
    {
        if (IsSuccess(result))
        {
            if (result.Value is not null)
            {
                _executionStatistics.Succeeded.Increment();
            }

            var syncActivity = result.Value is not null
                ? node.GetSyncActivityItem(result.Value)
                : node.GetSyncActivityItem(SyncActivityType.FetchUpdates);

            _syncActivity.OnSucceeded(syncActivity);
        }
        else if (IsSkipped(result))
        {
            _executionStatistics.Succeeded.Increment();

            var syncActivity = node.GetSyncActivityItem(SyncActivityType.FetchUpdates);

            _syncActivity.OnChanged(syncActivity, SyncActivityItemStatus.Skipped);
        }
        else
        {
            _executionStatistics.Failed.Increment();

            var syncActivity = node.GetSyncActivityItem(result.Value!);
            var (errorCode, errorMessage) = result.Exception!.GetErrorInfo();
            _syncActivity.OnChanged(syncActivity, SyncActivityItemStatus.InProgress);

            if (errorCode is FileSystemErrorCode.SharingViolation)
            {
                _syncActivity.OnWarning(syncActivity, errorCode, errorMessage);
            }
            else if (errorCode is FileSystemErrorCode.Cancelled)
            {
                _syncActivity.OnCancelled(syncActivity, errorCode);
            }
            else
            {
                _syncActivity.OnFailed(syncActivity, errorCode, errorMessage);
            }
        }
    }

    private void HandleEscape(IIdentifiable<TId> node)
    {
        var syncActivity = node.GetSyncActivityItem(SyncActivityType.FetchUpdates);

        _syncActivity.OnChanged(syncActivity, SyncActivityItemStatus.Skipped);
    }

    private bool IsSuccess(Result result)
    {
        return result.IsSuccess ||
               result.Exception is FileSystemClientException<TAltId>
               {
                   ErrorCode:
                   FileSystemErrorCode.PathBasedAccessNotSupported,
               };
    }

    private bool IsSkipped(Result result)
    {
        return result.Exception is FileSystemClientException<TAltId>
        {
            ErrorCode:
                   FileSystemErrorCode.DirectoryNotFound or
                   FileSystemErrorCode.PathNotFound or
                   FileSystemErrorCode.ObjectNotFound or
                   FileSystemErrorCode.IdentityMismatch or
                   FileSystemErrorCode.MetadataMismatch,
        };
    }
}
