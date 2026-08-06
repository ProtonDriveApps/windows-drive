using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.Operations;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection.StateBased.Enumeration;

internal class EnumerationFailureStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<EnumerationFailureStep<TId, TAltId>> _logger;
    private readonly AdapterTree<TId, TAltId> _adapterTree;

    private readonly IEqualityComparer<FileSystemNodeModel<TId>> _linkEqualityComparer = new FileSystemNodeModelLinkEqualityComparer<TId>();
    private readonly IEqualityComparer<FileSystemNodeModel<TId>> _attributesEqualityComparer = new FileSystemNodeModelAttributesEqualityComparer<TId>();
    private readonly AdapterTreeNodeModelMetadataEqualityComparer<TId, TAltId> _metadataEqualityComparer = new();

    public EnumerationFailureStep(
        ILogger<EnumerationFailureStep<TId, TAltId>> logger,
        AdapterTree<TId, TAltId> adapterTree)
    {
        _logger = logger;
        _adapterTree = adapterTree;
    }

    public void Execute(Exception exception, AdapterTreeNodeModel<TId, TAltId> initialNodeModel)
    {
        if (exception is not FileSystemClientException<TAltId> clientException)
        {
            throw new InvalidOperationException("Unexpected exception", exception);
        }

        var node = _adapterTree.NodeByIdOrDefault(initialNodeModel.Id);
        if (node is null)
        {
            // Tree node has been deleted
            Escape();
        }

        if (node.IsNodeOrBranchDeleted() ||
            (!node.Model.IsDirtyPlaceholder() && node.IsBranchDirty()))
        {
            // Branch has diverged
            return;
        }

        if (!_linkEqualityComparer.Equals(node.Model, initialNodeModel) ||
            !_attributesEqualityComparer.Equals(node.Model, initialNodeModel) ||
            !_metadataEqualityComparer.FileMetadataEquals(node.Model, initialNodeModel))
        {
            // Node has diverged
            return;
        }

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

                    // Marking grandparent directory with DirtyChildren flag, then the state-based
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

                    // Marking node with DirtyDeleted flag, then the state-based update detection will detect deletion
                    AppendDirtyFlags(failedNode, AdapterNodeStatus.DirtyDeleted);

                    // Marking parent directory with DirtyAttributes flag, then the state-based
                    // update detection will check if folder still exists.
                    AppendDirtyFlags(failedNode.Parent!, AdapterNodeStatus.DirtyAttributes);

                    break;

                    /* Nothing to update on the Adapter Tree */
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

    [DoesNotReturn]
    private static void Escape()
    {
        throw new EscapeException();
    }

    private void AppendDirtyFlags(AdapterTreeNode<TId, TAltId> node, AdapterNodeStatus flags)
    {
        if (node.Model.Status.HasFlag(flags))
        {
            return;
        }

        if (node.Model.Status.HasFlag(flags))
        {
            _logger.LogInformation("Adapter Tree {NodeType} node with Id={Id} already has ({DirtyFlags}) flag(s)", node.Type, node.Id, flags);
        }
        else
        {
            _logger.LogInformation("Adapter Tree {NodeType} node with Id={Id} got ({DirtyFlags}) flag(s)", node.Type, node.Id, flags);
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
