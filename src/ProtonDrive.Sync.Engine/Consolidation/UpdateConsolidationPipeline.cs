using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.Consolidation;

internal class UpdateConsolidationPipeline<TId>
    where TId : IEquatable<TId>, IComparable<TId>
{
    private readonly Replica _replica;
    private readonly SyncedTree<TId> _syncedTree;
    private readonly UpdateTree<TId> _updateTree;

    private readonly StateConsistencyGuard<TId> _stateConsistencyGuard;
    private readonly ConsolidationOperationFactory<TId> _consolidationOperationFactory;
    private readonly EffectiveOperationFilter<TId> _effectiveOperationFilter;
    private readonly DeletionOperationsFactory<TId> _deletionOperationsFactory;
    private readonly MissingUpdateTreeAncestorsFactory<TId> _missingAncestorsFactory;
    private readonly UnchangedUpdateTreeLeavesRemovalOperationsFactory<TId> _unchangedLeavesRemoval;

    private readonly OperationLogging<TId> _startedLogging;
    private readonly OperationLogging<TId> _finishedLogging;

    public UpdateConsolidationPipeline(
        Replica replica,
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> updateTree,
        ILogger<ConsolidationPipeline<TId>> logger)
    {
        _replica = replica;
        _syncedTree = syncedTree;
        _updateTree = updateTree;

        _stateConsistencyGuard = new StateConsistencyGuard<TId>(replica, syncedTree, updateTree, logger);
        _consolidationOperationFactory = new ConsolidationOperationFactory<TId>(replica);
        _effectiveOperationFilter = new EffectiveOperationFilter<TId>();
        _deletionOperationsFactory = new DeletionOperationsFactory<TId>(replica, syncedTree, updateTree);
        _missingAncestorsFactory = new MissingUpdateTreeAncestorsFactory<TId>(replica, syncedTree, updateTree);
        _unchangedLeavesRemoval = new UnchangedUpdateTreeLeavesRemovalOperationsFactory<TId>();

        _startedLogging = new OperationLogging<TId>($"Started consolidating {replica} operation", logger);
        _finishedLogging = new OperationLogging<TId>($"Finished consolidating {replica} operation", logger);
    }

    public void Execute(Operation<FileSystemNodeModel<TId>> detectedUpdate)
    {
        _startedLogging.LogOperation(detectedUpdate);

        var syncedNode = _syncedTree.NodeByOwnIdOrDefault(detectedUpdate.Model.Id, _replica);
        var node = _updateTree.NodeByIdOrDefault(detectedUpdate.Model.Id);

        var operation = ToConsolidationOperation(detectedUpdate, node, syncedNode);

        if (!HasEffect(operation, node))
        {
            return;
        }

        var prevParent = node?.Parent;

        if (IsDeletion(operation, node))
        {
            Execute(WithMissingParents(_deletionOperationsFactory.Operations(node)));
        }
        else
        {
            Execute(WithMissingParents(operation));

            RemoveUnchangedLeaves(node);
        }

        RemoveUnchangedLeaves(prevParent);

        _finishedLogging.LogOperation(detectedUpdate);
    }

    private Operation<UpdateTreeNodeModel<TId>>? ToConsolidationOperation(
        Operation<FileSystemNodeModel<TId>> detectedUpdate,
        UpdateTreeNode<TId>? node,
        SyncedTreeNode<TId>? syncedNode)
    {
        if (!_stateConsistencyGuard.IsConsistent(detectedUpdate, node, syncedNode))
        {
            // We ignore the detected update which is inconsistent with the Sync Engine state
            return null;
        }

        return _consolidationOperationFactory.Operation(detectedUpdate, node?.Model, syncedNode);
    }

    private bool HasEffect(
        [NotNullWhen(true)]
        Operation<UpdateTreeNodeModel<TId>>? operation,
        UpdateTreeNode<TId>? node)
    {
        return _effectiveOperationFilter.HasEffect(operation, node?.Model);
    }

    private bool IsDeletion(
        Operation<UpdateTreeNodeModel<TId>> operation,
        [NotNullWhen(true)]
        UpdateTreeNode<TId>? node)
    {
        return operation.Model.Status.Contains(UpdateStatus.Deleted) && node?.Model.Status.Contains(UpdateStatus.Deleted) == false;
    }

    private void RemoveUnchangedLeaves(UpdateTreeNode<TId>? node)
    {
        Execute(_unchangedLeavesRemoval.Operations(node));
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

    private void Execute(IEnumerable<Operation<UpdateTreeNodeModel<TId>>> operations)
    {
        _updateTree.Operations.Execute(operations);
    }
}
