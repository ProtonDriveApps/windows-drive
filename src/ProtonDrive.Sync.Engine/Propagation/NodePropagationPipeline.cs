using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Engine.Consolidation;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.Collections.Generic;
using ProtonDrive.Sync.Shared.ExecutionStatistics;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.Propagation;

internal class NodePropagationPipeline<TId> : INodePropagationPipeline<TId>
    where TId : struct, IEquatable<TId>
{
    private readonly Replica _replica;
    private readonly IScheduler _syncScheduler;
    private readonly ISyncAdapter<TId> _adapter;
    private readonly SyncedTree<TId> _syncedTree;
    private readonly UpdateTree<TId> _ownUpdateTree;
    private readonly UpdateTree<TId> _otherUpdateTree;
    private readonly PropagationTree<TId> _propagationTree;
    private readonly PropagatingNodes<TId> _propagatingNodes;
    private readonly ConcurrentExecutionStatistics _executionStatistics;

    private readonly OperationsFactory<TId> _operationsFactory;
    private readonly EqualizeOperationsFactory<UpdateTreeNodeModel<TId>, TId> _equalizeUpdateTreeOperationsFactory;
    private readonly UnchangedUpdateTreeLeavesRemovalOperationsFactory<TId> _unchangedOwnUpdateTreeLeavesRemoval;
    private readonly UnchangedUpdateTreeLeavesRemovalOperationsFactory<TId> _unchangedOtherUpdateTreeLeavesRemoval;
    private readonly MissingUpdateTreeAncestorsFactory<TId> _missingAncestorsFactory;
    private readonly DeletedChildrenRestorationPipeline<TId> _deletedChildrenRestoration;
    private readonly NameConflictResolutionPipeline<TId> _nameConflictResolution;

    public NodePropagationPipeline(
        Replica replica,
        IScheduler syncScheduler,
        ISyncAdapter<TId> adapter,
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> ownUpdateTree,
        UpdateTree<TId> otherUpdateTree,
        PropagationTree<TId> propagationTree,
        IFileNameFactory<TId> tempUniqueNameFactory,
        PropagatingNodes<TId> propagatingNodes,
        ConcurrentExecutionStatistics executionStatistics)
    {
        _replica = replica;
        _syncScheduler = syncScheduler;
        _adapter = adapter;
        _syncedTree = syncedTree;
        _ownUpdateTree = ownUpdateTree;
        _otherUpdateTree = otherUpdateTree;
        _propagationTree = propagationTree;
        _propagatingNodes = propagatingNodes;
        _executionStatistics = executionStatistics;

        _operationsFactory = new OperationsFactory<TId>();
        _equalizeUpdateTreeOperationsFactory = new EqualizeOperationsFactory<UpdateTreeNodeModel<TId>, TId>(new UpdateTreeNodeModelMetadataEqualityComparer<TId>());
        _unchangedOwnUpdateTreeLeavesRemoval = new UnchangedUpdateTreeLeavesRemovalOperationsFactory<TId>();
        _unchangedOtherUpdateTreeLeavesRemoval = new UnchangedUpdateTreeLeavesRemovalOperationsFactory<TId>();
        _missingAncestorsFactory = new MissingUpdateTreeAncestorsFactory<TId>(replica, syncedTree, ownUpdateTree);
        _deletedChildrenRestoration = new DeletedChildrenRestorationPipeline<TId>(replica, syncedTree, ownUpdateTree, propagationTree);

        _nameConflictResolution = new NameConflictResolutionPipeline<TId>(
            replica, syncScheduler, adapter, syncedTree, ownUpdateTree, propagationTree, tempUniqueNameFactory, propagatingNodes);
    }

    public async Task<ExecutionResultCode> ExecuteAsync(
        PropagationTreeNode<TId> node,
        Func<PropagationTreeNodeModel<TId>, UpdateStatus, UpdateStatus> statusFilter,
        CancellationToken cancellationToken)
    {
        // The node is "locked" to prevent parallel propagation of changes (operation execution)
        // to the same node. Parallel execution could happen in the NameConflictResolutionPipeline.
        var (success, result) = await _propagatingNodes.LockNodeAndExecute(
                node.Id,
                () => InternalExecuteAsync(node, statusFilter, cancellationToken))
            .ConfigureAwait(false);

        return success ? result : ExecutionResultCode.SkippedInternally;
    }

    private async Task<ExecutionResultCode> InternalExecuteAsync(
        PropagationTreeNode<TId> node,
        Func<PropagationTreeNodeModel<TId>, UpdateStatus, UpdateStatus> statusFilter,
        CancellationToken cancellationToken)
    {
        await foreach (var (status, operation) in OperationsToPropagate(node, statusFilter).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var result = await ExecuteOperationAsync(status, operation, cancellationToken).ConfigureAwait(false);

            if (result != ExecutionResultCode.Success)
            {
                return result;
            }
        }

        return ExecutionResultCode.Success;
    }

    private async Task<ExecutionResultCode> ExecuteOperationAsync(
        UpdateStatus status,
        ExecutableOperation<TId> operation,
        CancellationToken cancellationToken)
    {
        if (!await Schedule(() => CanAdjustSyncedTree(operation, status)).ConfigureAwait(false))
        {
            Failure(ExecutionResultCode.SkippedInternally);

            return ExecutionResultCode.SkippedInternally;
        }

        var result = await ExecuteOnAdapterAsync(operation, cancellationToken).ConfigureAwait(false);

        if (result != ExecutionResultCode.Success)
        {
            Failure(result);

            return result;
        }

        Success();

        await Schedule(() =>
            {
                var node = _propagationTree.NodeByOwnIdOrDefault(operation.Model.Id, _replica)!;

                AdjustSyncedTree(node, status);

                AdjustOtherUpdateTree(node, status);

                AdjustOwnUpdateTree(node, operation);

                RestoreDeletedChildren(node, status);

                AdjustPropagationTree(node, status);
            })
            .ConfigureAwait(false);

        return ExecutionResultCode.Success;
    }

    private void Success()
    {
        _executionStatistics.Succeeded.Increment();
    }

    private void Failure(ExecutionResultCode code)
    {
        if (code is ExecutionResultCode.DirtyBranch or
            ExecutionResultCode.DirtyNode or
            ExecutionResultCode.DirtyDestination or
            ExecutionResultCode.Offline or
            ExecutionResultCode.SkippedInternally or
            ExecutionResultCode.AccessRateLimitExceeded or
            ExecutionResultCode.Cancelled)
        {
            _executionStatistics.Skipped.Increment();
        }
        else
        {
            _executionStatistics.Failed.Increment();
        }
    }

    private IAsyncEnumerable<(UpdateStatus Status, ExecutableOperation<TId> Operation)> OperationsToPropagate(
        PropagationTreeNode<TId> node,
        Func<PropagationTreeNodeModel<TId>, UpdateStatus, UpdateStatus> filter)
    {
        return new ScheduledEnumerable<(UpdateStatus Status, ExecutableOperation<TId> Operation)>(
            _syncScheduler,
            Operations(node, filter));
    }

    private IEnumerable<(UpdateStatus Status, ExecutableOperation<TId> Operation)> Operations(
        PropagationTreeNode<TId> node,
        Func<PropagationTreeNodeModel<TId>, UpdateStatus, UpdateStatus> filter)
    {
        return StatusesToPropagate(node, filter)
            .Select(status => (status, PropagationOperation(node, status)));
    }

    private IEnumerable<UpdateStatus> StatusesToPropagate(
        PropagationTreeNode<TId> node,
        Func<PropagationTreeNodeModel<TId>, UpdateStatus, UpdateStatus> filter)
    {
        var status = filter(node.Model, OwnStatus(node.Model));

        return Statuses(status);
    }

    private ExecutableOperation<TId> PropagationOperation(
        PropagationTreeNode<TId> node,
        UpdateStatus status)
    {
        var originalNodeModel = (IFileSystemNodeModel<TId>?)_ownUpdateTree.NodeByIdOrDefault(node.Model.OwnId(_replica))?.Model
                                ?? ToOwnModel(_syncedTree.NodeByIdOrDefault(node.Model.Id));

        return _operationsFactory.Operation(ToOwnModel(node), originalNodeModel, status, node.Model.Backup);
    }

    private async Task<ExecutionResultCode> ExecuteOnAdapterAsync(
        ExecutableOperation<TId> operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        while ((await _adapter.ExecuteOperation(operation, cancellationToken).ConfigureAwait(false)) is var result)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (result is { Code: ExecutionResultCode.NameConflict, ConflictingNodeId: not null }
                && (await RenameConflictingNodeAsync(result.ConflictingNodeId.Value, cancellationToken).ConfigureAwait(false)).Succeeded())
            {
                continue;
            }

            return result.Code;
        }

        // ReSharper disable once HeuristicUnreachableCode
        throw new InvalidOperationException();
    }

    private void RestoreDeletedChildren(PropagationTreeNode<TId> propagationNode, UpdateStatus operationStatus)
    {
        if (operationStatus == UpdateStatus.Created &&
            propagationNode.Type == NodeType.Directory &&
            _deletedChildrenRestoration.ShouldRestore(propagationNode))
        {
            _deletedChildrenRestoration.Execute(propagationNode);
        }
    }

    private Task<bool> RenameConflictingNodeAsync(TId id, CancellationToken cancellationToken)
    {
        return _nameConflictResolution.ExecuteAsync(id, cancellationToken);
    }

    private bool CanAdjustSyncedTree(
        ExecutableOperation<TId> operation,
        UpdateStatus operationStatus)
    {
        if (operation.Model.AltId.Equals(default))
        {
            return true;
        }

        var node = _otherUpdateTree.NodeByIdOrDefault(operation.Model.AltId);
        if (node == null)
        {
            return true;
        }

        var statusToEqualize = operationStatus.Contains(UpdateStatus.Deleted)
            ? UpdateStatus.Deleted
            : node.Model.Status.Intersect(ExtendedStatus(operationStatus));

        if (statusToEqualize == UpdateStatus.Unchanged)
        {
            return true;
        }

        var propagationNode = _propagationTree.NodeByOwnIdOrDefault(operation.Model.Id, _replica)!;
        var nodeModel = new AltIdentifiableFileSystemNodeModel<TId, TId>()
            .CopiedFrom(node.Model)
            .WithId(propagationNode.Model.Id)
            .WithParentId(node.Model.ParentId);

        if (_replica == Replica.Local)
        {
            var syncedParentNode = _syncedTree.NodeByAltId(node.Parent!.Id);

            nodeModel = nodeModel.WithParentId(syncedParentNode.Id);
        }

        foreach (var status in Statuses(statusToEqualize))
        {
            var originalNode = _syncedTree.NodeByIdOrDefault(nodeModel.Id);

            var equalizeOperation = _operationsFactory.Operation(nodeModel, originalNode?.Model, status, false);

            if (equalizeOperation.Type != OperationType.Move || equalizeOperation.Model.ParentId.Equals(originalNode!.Model.ParentId))
            {
                continue;
            }

            var parentNode = _syncedTree.DirectoryById(nodeModel.ParentId);

            // Checking for cyclic move
            while (!parentNode.IsRoot)
            {
                if (parentNode == originalNode)
                {
                    return false;
                }

                parentNode = parentNode.Parent;
            }
        }

        return true;
    }

    private void AdjustSyncedTree(
        PropagationTreeNode<TId> propagationNode,
        UpdateStatus operationStatus)
    {
        var node = _otherUpdateTree.NodeByIdOrDefault(propagationNode.Model.OtherId(_replica));
        if (node == null)
        {
            return;
        }

        var statusToEqualize = operationStatus.Contains(UpdateStatus.Deleted)
            ? UpdateStatus.Deleted
            : node.Model.Status.Intersect(ExtendedStatus(operationStatus));

        if (statusToEqualize == UpdateStatus.Unchanged)
        {
            return;
        }

        var nodeModel = new AltIdentifiableFileSystemNodeModel<TId, TId>()
            .CopiedFrom(node.Model)
            .WithId(propagationNode.Model.Id)
            .WithParentId(node.Model.ParentId);

        if (_replica == Replica.Local)
        {
            var syncedParentNode = _syncedTree.NodeByAltId(node.Parent!.Id);

            nodeModel = nodeModel.WithParentId(syncedParentNode.Id);
        }

        foreach (var status in Statuses(statusToEqualize))
        {
            var originalNodeModel = _syncedTree.NodeByIdOrDefault(nodeModel.Id)?.Model;

            var operation = _operationsFactory.Operation(nodeModel, originalNodeModel, status, false);

            var syncedOperation = new Operation<SyncedTreeNodeModel<TId>>(
                operation.Type,
                new SyncedTreeNodeModel<TId>()
                    .CopiedFrom(operation.Model)
                    .WithAltId(propagationNode.AltId));

            _syncedTree.Operations.Execute(syncedOperation);
        }
    }

    private void AdjustOtherUpdateTree(
        PropagationTreeNode<TId> propagationNode,
        UpdateStatus operationStatus)
    {
        var node = _otherUpdateTree.NodeByIdOrDefault(propagationNode.Model.OtherId(_replica));
        if (node == null)
        {
            return;
        }

        var equalizeStatus = operationStatus.Contains(UpdateStatus.Deleted)
            ? UpdateStatus.Deleted
            : node.Model.Status.Intersect(ExtendedStatus(operationStatus));

        if (equalizeStatus == UpdateStatus.Unchanged)
        {
            return;
        }

        var status = equalizeStatus.Contains(UpdateStatus.Deleted)
            ? UpdateStatus.Unchanged
            : node.Model.Status.Minus(equalizeStatus);

        if (status == node.Model.Status)
        {
            return;
        }

        _otherUpdateTree.Operations.Execute(
            new Operation<UpdateTreeNodeModel<TId>>(
                OperationType.Update,
                node.Model.Copy().WithStatus(status)));

        RemoveUnchangedOtherUpdateTreeLeaves(node);
    }

    private void AdjustOwnUpdateTree(
        PropagationTreeNode<TId> propagationNode,
        ExecutableOperation<TId> operation)
    {
        var node = _ownUpdateTree.NodeByIdOrDefault(propagationNode.Model.OwnId(_replica));
        var syncedNode = _syncedTree.NodeByIdOrDefault(propagationNode.Model.Id);

        var nodeModel = operation.Model;
        var syncedNodeModel = ToOwnModel(syncedNode);

        var status = (node?.Model.Status ?? UpdateStatus.Unchanged).Intersect(UpdateStatus.Edited);

        if (syncedNodeModel == null)
        {
            status = operation.Type == OperationType.Delete ? UpdateStatus.Unchanged : UpdateStatus.Created;
        }
        else
        {
            if (operation.Type == OperationType.Delete)
            {
                status = UpdateStatus.Deleted;
            }
            else
            {
                if (operation.Type == OperationType.Edit)
                {
                    status = status.Minus(UpdateStatus.Edited);
                }

                if (nodeModel.Name != syncedNodeModel.Name)
                {
                    status = status.Union(UpdateStatus.Renamed);
                }

                if (!nodeModel.ParentId.Equals(syncedNodeModel.ParentId))
                {
                    status = status.Union(UpdateStatus.Moved);
                }
            }
        }

        if (node == null && status == UpdateStatus.Unchanged)
        {
            return;
        }

        var model = operation.Type != OperationType.Delete
            ? new UpdateTreeNodeModel<TId>().CopiedFrom(nodeModel).WithStatus(status)
            /* In case of Delete-ParentDelete (Pseudo) conflict own Update Tree might
            // contain descendants with Unchanged and Deleted update status.
            // The node and all descendants should be deleted. It should not contain descendants
            // having any other update status value except Unchanged and Deleted. */
            : null;

        var prevParent = node?.Parent;

        _ownUpdateTree.Operations.Execute(
            WithMissingParents(
                _equalizeUpdateTreeOperationsFactory.Operations(node?.Model, model)));

        RemoveUnchangedOwnUpdateTreeLeaves(node);

        RemoveUnchangedOwnUpdateTreeLeaves(prevParent);
    }

    private void AdjustPropagationTree(
        PropagationTreeNode<TId> node,
        UpdateStatus operationStatus)
    {
        var status = OwnStatus(node.Model).Minus(operationStatus);

        // Backup flag is relevant only for Edit operations, and Edit operation can be applied to one replica only.
        var backup = node.Model.Backup && operationStatus != UpdateStatus.Edited;

        _propagationTree.Operations.Execute(
            new Operation<PropagationTreeNodeModel<TId>>(
                OperationType.Update,
                WithOwnStatus(node.Model.Copy(), status).WithBackup(backup)));
    }

    private void RemoveUnchangedOwnUpdateTreeLeaves(
        UpdateTreeNode<TId>? node)
    {
        _ownUpdateTree.Operations.Execute(_unchangedOwnUpdateTreeLeavesRemoval.Operations(node));
    }

    private void RemoveUnchangedOtherUpdateTreeLeaves(
        UpdateTreeNode<TId>? node)
    {
        _otherUpdateTree.Operations.Execute(_unchangedOtherUpdateTreeLeavesRemoval.Operations(node));
    }

    private IEnumerable<Operation<UpdateTreeNodeModel<TId>>> WithMissingParents(
        IEnumerable<Operation<UpdateTreeNodeModel<TId>>> operations)
    {
        return operations.SelectMany(WithMissingParents);
    }

    private IEnumerable<Operation<UpdateTreeNodeModel<TId>>> WithMissingParents(
        Operation<UpdateTreeNodeModel<TId>> operation)
    {
        return _missingAncestorsFactory.WithMissingAncestors(operation);
    }

    private IEnumerable<UpdateStatus> Statuses(UpdateStatus status)
    {
        var renamedAndMoved = status.Intersect(UpdateStatus.RenamedAndMoved);

        return status.Minus(renamedAndMoved)
            .Split()
            .Prepend(renamedAndMoved)
            .Where(s => s != UpdateStatus.Unchanged);
    }

    private UpdateStatus ExtendedStatus(UpdateStatus status)
    {
        if (status.Contains(UpdateStatus.Created))
        {
            return UpdateStatus.Created | UpdateStatus.Edited | UpdateStatus.RenamedAndMoved;
        }

        return status;
    }

    private UpdateStatus OwnStatus(PropagationTreeNodeModel<TId> model) =>
        _replica == Replica.Remote ? model.RemoteStatus : model.LocalStatus;

    private PropagationTreeNodeModel<TId> WithOwnStatus(PropagationTreeNodeModel<TId> model, UpdateStatus value)
    {
        if (_replica == Replica.Remote)
        {
            model.RemoteStatus = value;
        }
        else
        {
            model.LocalStatus = value;
        }

        return model;
    }

    private FileSystemNodeModel<TId>? ToOwnModel(SyncedTreeNode<TId>? node)
    {
        if (node == null)
        {
            return null;
        }

        return _replica == Replica.Remote
            ? ToRemoteModel<SyncedTree<TId>, SyncedTreeNode<TId>, SyncedTreeNodeModel<TId>>(node)
            : ToLocalModel<SyncedTree<TId>, SyncedTreeNode<TId>, SyncedTreeNodeModel<TId>>(node);
    }

    private AltIdentifiableFileSystemNodeModel<TId, TId> ToOwnModel(PropagationTreeNode<TId> node)
    {
        return _replica == Replica.Remote
            ? ToRemoteModel<PropagationTree<TId>, PropagationTreeNode<TId>, PropagationTreeNodeModel<TId>>(node)
            : ToLocalModel<PropagationTree<TId>, PropagationTreeNode<TId>, PropagationTreeNodeModel<TId>>(node);
    }

    private AltIdentifiableFileSystemNodeModel<TId, TId> ToLocalModel<TTree, TNode, TModel>(TNode node)
        where TTree : FileSystemTree<TTree, TNode, TModel, TId>
        where TNode : FileSystemNode<TTree, TNode, TModel, TId>
        where TModel : AltIdentifiableFileSystemNodeModel<TId, TId>, new()
    {
        return new AltIdentifiableFileSystemNodeModel<TId, TId>()
            .CopiedFrom(node.Model);
    }

    private AltIdentifiableFileSystemNodeModel<TId, TId> ToRemoteModel<TTree, TNode, TModel>(TNode node)
        where TTree : FileSystemTree<TTree, TNode, TModel, TId>
        where TNode : FileSystemNode<TTree, TNode, TModel, TId>
        where TModel : AltIdentifiableFileSystemNodeModel<TId, TId>, new()
    {
        return new AltIdentifiableFileSystemNodeModel<TId, TId>()
            .CopiedFrom(node.Model)
            .WithId(node.Model.AltId)
            .WithAltId(node.Model.Id)
            .WithParentId(node.Parent!.Model.AltId);
    }

    private Task Schedule(Action origin)
    {
        return _syncScheduler.Schedule(origin);
    }

    private Task<T> Schedule<T>(Func<T> origin)
    {
        return _syncScheduler.Schedule(origin);
    }
}
