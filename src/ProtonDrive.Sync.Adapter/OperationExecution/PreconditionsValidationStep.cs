using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Adapter.Trees.Dirty;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Changes;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem.Traversal;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.OperationExecution;

internal sealed class PreconditionsValidationStep<TId, TAltId>
    where TId : struct, IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<PreconditionsValidationStep<TId, TAltId>> _logger;
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly DirtyTree<TId> _dirtyTree;
    private readonly IReadOnlyDictionary<TId, RootInfo<TAltId>> _syncRoots;
    private readonly IDetectedTreeChanges<TId> _detectedUpdates;

    private readonly PassiveTreeTraversal<DirtyTree<TId>, DirtyTreeNode<TId>, DirtyTreeNodeModel<TId>, TId> _dirtyTreeTraversal;

    public PreconditionsValidationStep(
        ILogger<PreconditionsValidationStep<TId, TAltId>> logger,
        AdapterTree<TId, TAltId> adapterTree,
        DirtyTree<TId> dirtyTree,
        IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots,
        IDetectedTreeChanges<TId> detectedUpdates)
    {
        _logger = logger;
        _adapterTree = adapterTree;
        _dirtyTree = dirtyTree;
        _syncRoots = syncRoots;
        _detectedUpdates = detectedUpdates;

        _dirtyTreeTraversal = new PassiveTreeTraversal<DirtyTree<TId>, DirtyTreeNode<TId>, DirtyTreeNodeModel<TId>, TId>();
    }

    public ExecutionResult<TId>? Execute(ExecutableOperation<TId> operation)
    {
        var node = _adapterTree.NodeByIdOrDefault(operation.Model.Id);
        var parentNode = operation.Type is OperationType.Create or OperationType.Move
            ? _adapterTree.NodeByIdOrDefault(operation.Model.ParentId)
            : null;

        return CreatingExistingNode(operation, node) ??
               DeletingNonExistingNode(operation, node) ??
               IsSyncRoot(operation, node) ??
               ReplicaIsOffline(operation, node, parentNode) ??
               SourceReplicaIsOffline(operation, node) ??
               BranchIsDirty(operation, node, parentNode) ??
               NodeOrParentIsDirty(operation, node, parentNode) ??
               DestinationBranchIsDirty(operation, node, parentNode) ??
               DeletingBranchWithDirtyNodes(operation, node) ??
               IsCyclicMove(operation, node, parentNode);
    }

    public ExecutionResult<TId>? ExecuteBeforeApplyingResult(ExecutableOperation<TId> operation)
    {
        var node = _adapterTree.NodeByIdOrDefault(operation.Model.Id);
        var parentNode = operation.Type is OperationType.Create or OperationType.Move
            ? _adapterTree.NodeByIdOrDefault(operation.Model.ParentId)
            : null;

        return DeletingNonExistingNode(operation, node) ??
               BranchIsDirtyBeforeApplyingResult(operation, node, parentNode) ??
               DestinationBranchIsDirtyBeforeApplyingResult(operation, node, parentNode);
    }

    public ExecutionResult<TId>? ExecuteBeforeNameConflict(
        ExecutableOperation<TId> operation)
    {
        var node = _adapterTree.NodeByIdOrDefault(operation.Model.Id);
        var parentNode = operation.Type is OperationType.Create or OperationType.Move
            ? _adapterTree.NodeByIdOrDefault(operation.Model.ParentId)
            : null;

        return BranchIsDirty(operation, node, parentNode) ??
               DestinationBranchIsDirty(operation, node, parentNode);
    }

    private ExecutionResult<TId>? CreatingExistingNode(
        ExecutableOperation<TId> operation,
        AdapterTreeNode<TId, TAltId>? node)
    {
        if (operation.Type == OperationType.Create && node != null)
        {
            // Creating already existing node silently succeeds.
            _logger.LogDebug("Node with Id={Id} already exists", operation.Model.Id);

            return ExecutionResult<TId>.Success();
        }

        return default;
    }

    private ExecutionResult<TId>? DeletingNonExistingNode(
        ExecutableOperation<TId> operation,
        AdapterTreeNode<TId, TAltId>? node)
    {
        if (operation.Type == OperationType.Delete && node == null)
        {
            // Deleting not existing node silently succeeds.
            _logger.LogDebug("Node with Id={Id} does not exist", operation.Model.Id);

            return ExecutionResult<TId>.Success();
        }

        return default;
    }

    private ExecutionResult<TId>? IsSyncRoot(
        ExecutableOperation<TId> operation,
        AdapterTreeNode<TId, TAltId>? node)
    {
        // Manipulating first level node (sync root) is not allowed
        if (node?.IsSyncRoot() == true)
        {
            _logger.LogDebug("Node with Id={Id} is sync root", operation.Model.Id);

            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyNode);
        }

        // Creating first level node or moving to root is not allowed
        if (operation.Type is OperationType.Create or OperationType.Move &&
            operation.Model.ParentId.Equals(_adapterTree.Root.Id))
        {
            _logger.LogDebug("Parent node with Id={Id} is root", operation.Model.ParentId);

            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyDestination);
        }

        return default;
    }

    private ExecutionResult<TId>? ReplicaIsOffline(
        ExecutableOperation<TId> operation,
        AdapterTreeNode<TId, TAltId>? node,
        AdapterTreeNode<TId, TAltId>? parentNode)
    {
        // The method does not apply to the source replica of Move operations
        if (parentNode == null && (node == null || (operation.Type == OperationType.Move && !node.Model.ParentId.Equals(operation.Model.ParentId))))
        {
            return default;
        }

        var syncRootNode = parentNode?.GetSyncRoot() ?? node?.GetSyncRoot() ?? throw new InvalidOperationException("Not possible");
        var syncRoot = _syncRoots[syncRootNode.Id];

        if (!syncRoot.IsEnabled)
        {
            _logger.LogDebug("The sync root with Id={Id} is disabled", syncRoot.Id);

            return ExecutionResult<TId>.Failure(ExecutionResultCode.Offline);
        }

        return default;
    }

    private ExecutionResult<TId>? SourceReplicaIsOffline(
        ExecutableOperation<TId> operation,
        AdapterTreeNode<TId, TAltId>? node)
    {
        // The method only applies to the source replica of Move operations
        if (node == null || operation.Type != OperationType.Move || node.Model.ParentId.Equals(operation.Model.ParentId))
        {
            return default;
        }

        var syncRoot = _syncRoots[node.GetSyncRoot().Id];
        if (!syncRoot.IsEnabled)
        {
            _logger.LogDebug("The source sync root with Id={Id} is disabled", syncRoot.Id);

            // Operations are executed on the destination replica. Therefore,
            // we do not return Offline for the disabled source replica, as
            // it would be interpreted as the destination replica being offline.
            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyNode);
        }

        return default;
    }

    private ExecutionResult<TId>? BranchIsDirty(
        ExecutableOperation<TId> operation,
        AdapterTreeNode<TId, TAltId>? node,
        AdapterTreeNode<TId, TAltId>? parentNode)
    {
        if (node == null && operation.Type != OperationType.Create)
        {
            _logger.LogWarning("The node with Id={Id} does not exist", operation.Model.Id);

            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyBranch);
        }

        var parent = operation.Type == OperationType.Create ? parentNode : node?.Parent;

        if (parent == null)
        {
            _logger.LogWarning("The parent folder with Id={Id} does not exist", operation.Model.ParentId);

            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyBranch);
        }

        if (parent.FromNodeToRoot().Any(n => n.Model.HasDirtyDescendantsFlag()))
        {
            _logger.LogDebug("The branch is dirty");

            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyBranch);
        }

        if (parent.FromNodeToRoot().Any(n => n.Model.IsLostOrDeleted()))
        {
            _logger.LogDebug("The branch is lost or deleted");

            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyBranch);
        }

        return default;
    }

    private ExecutionResult<TId>? BranchIsDirtyBeforeApplyingResult(
        ExecutableOperation<TId> operation,
        AdapterTreeNode<TId, TAltId>? node,
        AdapterTreeNode<TId, TAltId>? parentNode)
    {
        if (node == null && operation.Type != OperationType.Create)
        {
            _logger.LogWarning("The node with Id={Id} does not exist", operation.Model.Id);

            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyBranch);
        }

        var parent = operation.Type == OperationType.Create ? parentNode : node?.Parent;

        if (parent == null)
        {
            _logger.LogWarning("The parent folder with Id={Id} does not exist", operation.Model.ParentId);

            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyBranch);
        }

        return default;
    }

    private ExecutionResult<TId>? NodeOrParentIsDirty(
        ExecutableOperation<TId> operation,
        AdapterTreeNode<TId, TAltId>? node,
        AdapterTreeNode<TId, TAltId>? parentNode)
    {
        node ??= parentNode ?? throw new InvalidOperationException("Both node and parent node are NULL");

        if (node.Model.IsDirtyPlaceholder())
        {
            throw new InvalidOperationException($"The node with Id={node.Id} is dirty placeholder");
        }

        if (operation.Type is OperationType.Create or OperationType.Edit or OperationType.Delete &&
            node.Model.Status.HasAnyFlag(AdapterNodeStatus.DirtyAttributes))
        {
            _logger.LogDebug("The node with Id={Id} is dirty", node.Id);

            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyNode);
        }

        if (node.Model.IsLostOrDeleted())
        {
            _logger.LogDebug("The node with Id={Id} is lost or deleted", node.Id);

            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyNode);
        }

        if (_detectedUpdates.Contains(node.Model.Id))
        {
            _logger.LogDebug("The node with Id={Id} has updates detected", node.Id);

            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyNode);
        }

        return default;
    }

    private ExecutionResult<TId>? DestinationBranchIsDirty(
        ExecutableOperation<TId> operation,
        AdapterTreeNode<TId, TAltId>? node,
        AdapterTreeNode<TId, TAltId>? parentNode)
    {
        if (node == null || operation.Type != OperationType.Move || node.Model.ParentId.Equals(operation.Model.ParentId))
        {
            return default;
        }

        if (parentNode == null)
        {
            _logger.LogWarning("The destination folder with Id={Id} does not exist", operation.Model.ParentId);

            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyDestination);
        }

        if (parentNode.FromNodeToRoot().Any(n => n.Model.HasDirtyDescendantsFlag()))
        {
            _logger.LogDebug("The destination branch is dirty");

            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyDestination);
        }

        if (parentNode.FromNodeToRoot().Any(n => n.Model.IsLostOrDeleted()))
        {
            _logger.LogDebug("The destination branch is lost or deleted");

            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyDestination);
        }

        return default;
    }

    private ExecutionResult<TId>? DestinationBranchIsDirtyBeforeApplyingResult(
        ExecutableOperation<TId> operation,
        AdapterTreeNode<TId, TAltId>? node,
        AdapterTreeNode<TId, TAltId>? parentNode)
    {
        if (node == null || operation.Type != OperationType.Move || node.Model.ParentId.Equals(operation.Model.ParentId))
        {
            return default;
        }

        if (parentNode == null)
        {
            _logger.LogWarning("The destination folder with Id={Id} does not exist", operation.Model.ParentId);

            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyDestination);
        }

        return default;
    }

    private ExecutionResult<TId>? DeletingBranchWithDirtyNodes(
        ExecutableOperation<TId> operation,
        AdapterTreeNode<TId, TAltId>? node)
    {
        if (node == null || node.IsLeaf || operation.Type != OperationType.Delete)
        {
            return default;
        }

        var dirtyNode = _dirtyTree.NodeByIdOrDefault(node.Id);
        if (dirtyNode == null)
        {
            // There is no dirty descendants
            return default;
        }

        if (_dirtyTreeTraversal.IncludeStartingNode().PreOrder(dirtyNode).Any(n =>
                n.Model.Status.HasAnyFlag(AdapterNodeStatus.DirtyPlaceholder |
                                          AdapterNodeStatus.DirtyAttributes |
                                          AdapterNodeStatus.DirtyParent |
                                          AdapterNodeStatus.DirtyChildren)))
        {
            _logger.LogDebug("The branch starting at node with Id={Id} contains dirty or lost nodes", node.Id);

            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyNode);
        }

        return default;
    }

    private ExecutionResult<TId>? IsCyclicMove(
        ExecutableOperation<TId> operation,
        AdapterTreeNode<TId, TAltId>? node,
        AdapterTreeNode<TId, TAltId>? parentNode)
    {
        if (node == null ||
            parentNode == null ||
            operation.Type != OperationType.Move ||
            node.Model.ParentId.Equals(operation.Model.ParentId) ||
            node.Type != NodeType.Directory)
        {
            return default;
        }

        if (parentNode.FromParentToRoot().Any(n => n.Id.Equals(node.Model.Id)))
        {
            _logger.LogWarning("Moving node with Id={Id} to parent with Id={ParentId} is a cyclic move", node.Id, parentNode.Id);

            return ExecutionResult<TId>.Failure(ExecutionResultCode.DirtyDestination);
        }

        return default;
    }
}
