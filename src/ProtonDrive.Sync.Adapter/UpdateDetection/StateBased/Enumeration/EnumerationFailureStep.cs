using System;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.UpdateDetection.StateBased.Enumeration;

internal class EnumerationFailureStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<EnumerationFailureStep<TId, TAltId>> _logger;
    private readonly AdapterTree<TId, TAltId> _adapterTree;

    public EnumerationFailureStep(
        ILogger<EnumerationFailureStep<TId, TAltId>> logger,
        AdapterTree<TId, TAltId> adapterTree)
    {
        _logger = logger;
        _adapterTree = adapterTree;
    }

    public void Execute(Exception exception, AdapterTreeNode<TId, TAltId> node)
    {
        if (exception is not FileSystemClientException<TAltId> clientException)
        {
            throw new InvalidOperationException("Unexpected exception", exception);
        }

        EscapeIfDeleted(node);

        var id = clientException.ObjectId;

        var failedNode = !(id is null || id.Equals(default))
            ? _adapterTree.NodeByAltIdOrDefault((node.GetVolumeId(), id))
            : null;

        if (failedNode != null)
        {
            if (failedNode.IsRoot)
            {
                throw new InvalidOperationException("The root node should not have AltId value");
            }

            switch (clientException.ErrorCode)
            {
                // The part of the specified path cannot be found
                case FileSystemErrorCode.DirectoryNotFound:
                    /* It's not clear whether the node parent or one of its higher level ancestors has diverged.*/

                    if (failedNode.IsSyncRoot() || failedNode.Parent.IsSyncRoot())
                    {
                        _logger.LogWarning("The sync root folder cannot be found");

                        break;
                    }

                    // Marking grand parent directory with DirtyChildren flag, then the state-based
                    // update detection will enumerate the changes.
                    AppendDirtyFlags(failedNode.Parent!.Parent!, AdapterNodeStatus.DirtyChildren);

                    break;

                // The file system object at the specified path cannot be found
                case FileSystemErrorCode.PathNotFound:
                    /* It's not clear whether the parent directory identity has expected value, so we
                    // don't know whether the node state has diverged or the state of one of its ancestors.*/

                    if (failedNode.IsSyncRoot())
                    {
                        _logger.LogWarning("The sync root folder cannot be found");

                        break;
                    }

                    // Marking parent directory with DirtyChildren flag, then the state-based
                    // update detection will enumerate the changes.
                    AppendDirtyFlags(failedNode.Parent!, AdapterNodeStatus.DirtyChildren);

                    break;

                // The file system object identity does not match the expected.
                case FileSystemErrorCode.IdentityMismatch:
                    /* It's not clear whether the parent directory identity has expected value, so we
                    // don't know whether the node state has diverged or the state of one of its ancestors.*/

                    if (failedNode.IsSyncRoot())
                    {
                        _logger.LogWarning("The sync root folder cannot be found");

                        break;
                    }

                    // Marking parent directory with DirtyChildren flag, then the state-based
                    // update detection will enumerate the changes.
                    AppendDirtyFlags(failedNode.Parent!, AdapterNodeStatus.DirtyChildren);

                    break;

                // The file system object state has diverged from the expected
                case FileSystemErrorCode.MetadataMismatch:

                    // Marking node with DirtyAttributes flag, then the state-based
                    // update detection will enumerate the changes.
                    AppendDirtyFlags(failedNode, AdapterNodeStatus.DirtyAttributes);

                    break;

                // The file system object has been deleted
                case FileSystemErrorCode.ObjectNotFound:
                    /* It's not clear whether the node or one of its ancestors has been deleted.*/

                    if (failedNode.IsSyncRoot())
                    {
                        _logger.LogWarning("The sync root folder cannot be found");

                        break;
                    }

                    // Marking parent directory with DirtyChildren flag, then the state-based
                    // update detection will enumerate the changes.
                    AppendDirtyFlags(failedNode.Parent!, AdapterNodeStatus.DirtyChildren);

                    break;

                // The file system object is in use.
                // On Windows local file systems might be randomly thrown when the file system is under high load.
                case FileSystemErrorCode.SharingViolation:
                    /* Nothing to update on the Adapter Tree */

                    break;

                case FileSystemErrorCode.UnauthorizedAccess:
                    /* Nothing to update on the Adapter Tree */

                    break;
            }
        }

        if (clientException.ErrorCode is FileSystemErrorCode.PathBasedAccessNotSupported &&
            node.Model.IsDirtyPlaceholder())
        {
            // The file system does not support path based access. Deleting the dirty placeholder.
            ExecuteOnTree(new Operation<AdapterTreeNodeModel<TId, TAltId>>(
                    OperationType.Delete, new AdapterTreeNodeModel<TId, TAltId>().WithId(node.Model.Id)));
        }
        else if (clientException.ErrorCode is FileSystemErrorCode.PathNotFound &&
                 node.Model.IsDirtyPlaceholder())
        {
            // The file system object with the expected name does not exist in the folder. Deleting the dirty placeholder.
            ExecuteOnTree(new Operation<AdapterTreeNodeModel<TId, TAltId>>(
                OperationType.Delete, new AdapterTreeNodeModel<TId, TAltId>().WithId(node.Model.Id)));
        }
    }

    private void EscapeIfDeleted(AdapterTreeNode<TId, TAltId> node)
    {
        if (node.IsDeleted)
        {
            Escape();
        }
    }

    private void Escape()
    {
        throw new EscapeException();
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
