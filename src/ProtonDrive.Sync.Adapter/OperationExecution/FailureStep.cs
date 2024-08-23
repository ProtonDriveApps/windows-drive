using System;
using System.IO;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Adapter.OnDemandHydration;
using ProtonDrive.Sync.Adapter.Shared;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.OperationExecution;

internal class FailureStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<FailureStep<TId, TAltId>> _logger;
    private readonly AdapterTree<TId, TAltId> _adapterTree;

    public FailureStep(
        ILogger<FailureStep<TId, TAltId>> logger,
        AdapterTree<TId, TAltId> adapterTree)
    {
        _logger = logger;
        _adapterTree = adapterTree;
    }

    public ExecutionResultCode Execute(
        Exception exception,
        NodeInfo<TAltId> nodeInfo,
        NodeInfo<TAltId>? destinationInfo)
    {
        switch (exception)
        {
            // Operation has been cancelled
            case OperationCanceledException:

                return ExecutionResultCode.Cancelled;

            // The file has been modified very recently, and it will be ignored/skipped
            case FileRevisionProviderException { ErrorCode: FileSystemErrorCode.LastWriteTimeTooRecent }:
                return ExecutionResultCode.DirtyNode;

            // The error occurred while copying the file content
            case FileRevisionProviderException:

            // The error occurred while copying the file content
            case IOException:
                return ExecutionResultCode.Error;

            // Other file system object with the duplicate name already exists
            case FileSystemClientException { ErrorCode: FileSystemErrorCode.DuplicateName }:
                return ExecutionResultCode.NameConflict;

            // The new file system object name is invalid
            case FileSystemClientException { ErrorCode: FileSystemErrorCode.InvalidName }:
                return ExecutionResultCode.Error;

            // Attempted to add a child to the folder with max allowed number of children
            case FileSystemClientException { ErrorCode: FileSystemErrorCode.TooManyChildren }:
                return ExecutionResultCode.Error;

            // The replica is offline
            case FileSystemClientException { ErrorCode: FileSystemErrorCode.Offline }:
                return ExecutionResultCode.Offline;

            case HydrationException:
                return ExecutionResultCode.Error;
        }

        if (exception is not FileSystemClientException)
        {
            throw new InvalidOperationException("Unexpected exception", exception);
        }

        if (exception is not FileSystemClientException<TAltId> clientException)
        {
            return ExecutionResultCode.Error;
        }

        var id = clientException.ObjectId;

        var failedNode = !(id is null || id.Equals(default))
            ? _adapterTree.NodeByAltIdOrDefault((nodeInfo.GetCompoundId().VolumeId, id))
            : null;

        if (failedNode == null)
        {
            // Nothing to update in the Adapter Tree
            return ExecutionResultCode.Error;
        }

        if (failedNode.IsRoot)
        {
            throw new InvalidOperationException("The root node should not have AltId value");
        }

        // Assume all is happening on the same volume
        var dirtyDestination = destinationInfo != null &&
                               !clientException.ObjectId!.Equals(nodeInfo.Id) &&
                               !clientException.ObjectId.Equals(nodeInfo.ParentId) &&
                               (clientException.ObjectId.Equals(destinationInfo.Id) ||
                                clientException.ObjectId.Equals(destinationInfo.ParentId));

        switch (clientException.ErrorCode)
        {
            // The part of the specified path cannot be found
            case FileSystemErrorCode.DirectoryNotFound:
                /* It's not clear whether the node parent or one of its higher level ancestors has diverged.*/

                if (failedNode.IsSyncRoot() || failedNode.Parent.IsSyncRoot())
                {
                    _logger.LogWarning("The sync root folder cannot be found");

                    return ExecutionResultCode.Error;
                }

                // Marking grand parent directory with DirtyChildren flag, then the state-based
                // update detection will enumerate the changes.
                AppendDirtyFlags(failedNode.Parent!.Parent!, AdapterNodeStatus.DirtyChildren);

                return dirtyDestination ? ExecutionResultCode.DirtyDestination : ExecutionResultCode.DirtyBranch;

            // The file system object at the specified path cannot be found
            case FileSystemErrorCode.PathNotFound:
                /* It's not clear whether the parent directory identity has expected value, so we
                // don't know whether the node state has diverged or the state of one of its ancestors.*/

                if (failedNode.IsSyncRoot())
                {
                    _logger.LogWarning("The sync root folder cannot be found");

                    return ExecutionResultCode.Error;
                }

                // Marking parent directory with DirtyChildren flag, then the state-based
                // update detection will enumerate the changes.
                AppendDirtyFlags(failedNode.Parent!, AdapterNodeStatus.DirtyChildren);

                return dirtyDestination ? ExecutionResultCode.DirtyDestination : ExecutionResultCode.DirtyBranch;

            // The file system object identity does not match the expected.
            case FileSystemErrorCode.IdentityMismatch:
                /* It's not clear whether the parent directory identity has expected value, so we
                // don't know whether the node state has diverged or the state of one of its ancestors. */

                if (failedNode.IsSyncRoot())
                {
                    _logger.LogWarning("The sync root folder cannot be found");

                    return ExecutionResultCode.Error;
                }

                // Marking parent directory with DirtyChildren flag, then the state-based
                // update detection will enumerate the changes.
                AppendDirtyFlags(failedNode.Parent!, AdapterNodeStatus.DirtyChildren);

                return dirtyDestination ? ExecutionResultCode.DirtyDestination : ExecutionResultCode.DirtyBranch;

            // The file system object state has diverged from the expected
            case FileSystemErrorCode.MetadataMismatch:

                // Marking node with DirtyAttributes flag, then the state-based
                // update detection will enumerate the changes.
                AppendDirtyFlags(failedNode, AdapterNodeStatus.DirtyAttributes);

                return dirtyDestination ? ExecutionResultCode.DirtyDestination : ExecutionResultCode.DirtyNode;

            // The file system object has been deleted
            case FileSystemErrorCode.ObjectNotFound:
                /* It's not clear whether the node or one of its ancestors has been deleted. */

                if (failedNode.IsSyncRoot())
                {
                    _logger.LogWarning("The sync root folder cannot be found");

                    return ExecutionResultCode.Error;
                }

                // Marking parent directory with DirtyChildren flag, then the state-based
                // update detection will enumerate the changes.
                AppendDirtyFlags(failedNode.Parent!, AdapterNodeStatus.DirtyChildren);

                return dirtyDestination ? ExecutionResultCode.DirtyDestination : ExecutionResultCode.DirtyBranch;

            // The file system object is in use.
            // On Windows local file systems might be randomly thrown when the file system is under high load.
            case FileSystemErrorCode.SharingViolation:
                /* Nothing to update in the Adapter Tree */

                return dirtyDestination ? ExecutionResultCode.DirtyDestination : ExecutionResultCode.DirtyNode;

            case FileSystemErrorCode.UnauthorizedAccess:
                /* Nothing to update in the Adapter Tree */

                return dirtyDestination ? ExecutionResultCode.DirtyDestination : ExecutionResultCode.DirtyNode;

            // Unspecified error occurred
            case FileSystemErrorCode.Unknown:

                // Marking node with DirtyAttributes flag, then the state-based
                // update detection will enumerate the changes.
                AppendDirtyFlags(failedNode, AdapterNodeStatus.DirtyAttributes);

                return ExecutionResultCode.Error;

            // There was an integrity verification failure in writing file contents
            case FileSystemErrorCode.IntegrityFailure:
                /* Nothing to update in the Adapter Tree */

                return dirtyDestination ? ExecutionResultCode.DirtyDestination : ExecutionResultCode.DirtyNode;

            // Other expected error
            default:
                /* Nothing to update in the Adapter Tree */

                return ExecutionResultCode.Error;
        }
    }

    private void AppendDirtyFlags(AdapterTreeNode<TId, TAltId> node, AdapterNodeStatus flags)
    {
        if (node.Model.Status.HasFlag(flags))
        {
            return;
        }

        ExecuteOnTree(new Operation<AdapterTreeNodeModel<TId, TAltId>>(
            OperationType.Update,
            node.Model.Copy()
                .WithDirtyFlags(flags)));
    }

    private void ExecuteOnTree(Operation<AdapterTreeNodeModel<TId, TAltId>> operation)
    {
        _adapterTree.Operations.LogAndExecute(_logger, operation);
    }
}
