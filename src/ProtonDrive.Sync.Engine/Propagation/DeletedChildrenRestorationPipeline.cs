using System;
using System.Collections.Generic;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Engine.ConflictResolution;
using ProtonDrive.Sync.Engine.Consolidation;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.Propagation;

internal class DeletedChildrenRestorationPipeline<TId>
    where TId : IEquatable<TId>
{
    private readonly Replica _replica;
    private readonly SyncedTree<TId> _syncedTree;
    private readonly UpdateTree<TId> _ownUpdateTree;
    private readonly PropagationTree<TId> _propagationTree;

    private readonly MissingUpdateTreeAncestorsFactory<TId> _missingOwnUpdateTreeAncestorsFactory;
    private readonly MissingPropagationTreeAncestorsFactory<TId> _missingPropagationTreeAncestorsFactory;
    private readonly EqualizeOperationsFactory<UpdateTreeNodeModel<TId>, TId> _equalizeUpdateTreeOperationsFactory;
    private readonly EqualizeOperationsFactory<PropagationTreeNodeModel<TId>, TId> _equalizePropagationTreeOperationsFactory;

    public DeletedChildrenRestorationPipeline(
        Replica replica,
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> ownUpdateTree,
        PropagationTree<TId> propagationTree)
    {
        _replica = replica;
        _syncedTree = syncedTree;
        _ownUpdateTree = ownUpdateTree;
        _propagationTree = propagationTree;

        _missingOwnUpdateTreeAncestorsFactory = new MissingUpdateTreeAncestorsFactory<TId>(replica, syncedTree, ownUpdateTree);
        _missingPropagationTreeAncestorsFactory = new MissingPropagationTreeAncestorsFactory<TId>(syncedTree, propagationTree);
        _equalizeUpdateTreeOperationsFactory = new EqualizeOperationsFactory<UpdateTreeNodeModel<TId>, TId>(new UpdateTreeNodeModelMetadataEqualityComparer<TId>());
        _equalizePropagationTreeOperationsFactory = new EqualizeOperationsFactory<PropagationTreeNodeModel<TId>, TId>(new PropagationTreeNodeModelMetadataEqualityComparer<TId>());
    }

    public bool ShouldRestore(PropagationTreeNode<TId> propagationNode)
    {
        return propagationNode.Model.OwnStatus(_replica).Contains(UpdateStatus.Created | UpdateStatus.Restore);
    }

    /// <summary>
    /// Restores branch deleted by the user on "own" replica.
    /// </summary>
    /// <param name="startingNode">Propagation Tree node to restore deleted children.</param>
    public void Execute(PropagationTreeNode<TId> startingNode)
    {
        Ensure.IsFalse(startingNode.IsRoot, "Root node cannot be restored");

        var syncedNode = _syncedTree.NodeByIdOrDefault(startingNode.Id)
                         ?? throw new InvalidOperationException($"SyncedTree node with Id={startingNode.Id} does not exist");

        foreach (var childNode in syncedNode.Children)
        {
            if (MovedOutOfTheBranch(childNode))
            {
                // Should be already restored
            }
            else if (PseudoDeleted(childNode))
            {
                CreateDeletedOwnUpdateTreeNode(childNode);
            }
            else
            {
                RestoreDeletedNode(childNode);
                CreateDeletedOwnUpdateTreeNode(childNode);
            }
        }
    }

    private bool MovedOutOfTheBranch(SyncedTreeNode<TId> syncedNode)
    {
        var ownUpdateTreeNode = _ownUpdateTree.NodeByIdOrDefault(syncedNode.Model.OwnId(_replica));
        if (ownUpdateTreeNode != null)
        {
            // If node exists in the own Update Tree, it has survived deletion by been moved outside
            // of the deleted branch on the own replica.
            return true;
        }

        return false;
    }

    private bool PseudoDeleted(SyncedTreeNode<TId> syncedNode)
    {
        var propagationNode = _propagationTree.NodeByIdOrDefault(syncedNode.Id);

        // Indirect Delete-Delete (Pseudo) conflict
        return propagationNode != null &&
               propagationNode.Model.LocalStatus.Contains(UpdateStatus.Deleted) &&
               propagationNode.Model.RemoteStatus.Contains(UpdateStatus.Deleted);
    }

    /// <summary>
    /// Creates a node with Deleted update status on "own" Update Tree.
    /// </summary>
    private void CreateDeletedOwnUpdateTreeNode(SyncedTreeNode<TId> syncedNode)
    {
        var targetNode = _ownUpdateTree.NodeByIdOrDefault(syncedNode.Model.OwnId(_replica));
        if (targetNode != null)
        {
            throw new InvalidOperationException();
        }

        var model = new UpdateTreeNodeModel<TId>()
            .CopiedFrom(syncedNode.Model)
            .WithId(syncedNode.Model.OwnId(_replica))
            .WithParentId(syncedNode.Parent!.Model.OwnId(_replica));

        model = model.WithStatus(UpdateStatus.Deleted | UpdateStatus.Restore);

        EqualizeOwnUpdateTreeNode(null, model);
    }

    private void RestoreDeletedNode(SyncedTreeNode<TId> syncedNode)
    {
        var propagationNode = _propagationTree.NodeByIdOrDefault(syncedNode.Id);

        AdjustPropagationTreeNode(propagationNode, syncedNode);
    }

    private void AdjustPropagationTreeNode(PropagationTreeNode<TId>? targetNode, SyncedTreeNode<TId> syncedNode)
    {
        var model = targetNode != null
            ? targetNode.Model.Copy()
            : new PropagationTreeNodeModel<TId>()
                .CopiedFrom(syncedNode.Model);

        model = model.WithOwnStatus(UpdateStatus.Created | UpdateStatus.Restore, _replica);

        EqualizePropagationNode(targetNode, model);
    }

    private void EqualizeOwnUpdateTreeNode(UpdateTreeNode<TId>? targetNode, UpdateTreeNodeModel<TId> model)
    {
        _ownUpdateTree.Operations.Execute(
            WithMissingOwnAncestors(
                _equalizeUpdateTreeOperationsFactory.Operations(targetNode?.Model, model)));
    }

    private void EqualizePropagationNode(PropagationTreeNode<TId>? targetNode, PropagationTreeNodeModel<TId> model)
    {
        _propagationTree.Operations.Execute(
            WithMissingAncestors(
                _equalizePropagationTreeOperationsFactory.Operations(targetNode?.Model, model)));
    }

    private IEnumerable<Operation<UpdateTreeNodeModel<TId>>> WithMissingOwnAncestors(
        IEnumerable<Operation<UpdateTreeNodeModel<TId>>> operations)
    {
        return _missingOwnUpdateTreeAncestorsFactory.WithMissingAncestors(operations);
    }

    private IEnumerable<Operation<PropagationTreeNodeModel<TId>>> WithMissingAncestors(
        IEnumerable<Operation<PropagationTreeNodeModel<TId>>> operations)
    {
        return _missingPropagationTreeAncestorsFactory.WithMissingAncestors(operations);
    }
}
