using Proton.Drive.Sdk.Sync.Adapter.Shared;
using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.Adapter.UpdateDetection.StateBased.Enumeration;
using Proton.Drive.Sdk.Sync.Shared.ExecutionStatistics;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.SyncActivity;
using Proton.Drive.Shared;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection.StateBased;

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
                ? node.GetSyncActivityItemForUpdateDetection(result.Value)
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

            var syncActivity = node.GetSyncActivityItemForUpdateDetection(result.Value!);
            var (errorCode, errorMessage) = result.Exception!.GetErrorInfo();
            _syncActivity.OnProgress(syncActivity, Progress.Zero);

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
