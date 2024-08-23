using System;
using System.Collections.Generic;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Engine.ConflictResolution;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.Reconciliation;

internal class ApplyToPropagationTreePipeline<TId>
    where TId : IComparable<TId>, IEquatable<TId>
{
    private readonly PropagationTree<TId> _propagationTree;

    private readonly MissingPropagationTreeAncestorsFactory<TId> _missingAncestorsFactory;
    private readonly UnchangedPropagationTreeLeavesRemovalOperationsFactory<TId> _leavesRemovalOperationsFactory;
    private readonly EqualizeOperationsFactory<PropagationTreeNodeModel<TId>, TId> _equalizeOperationsFactory;

    public ApplyToPropagationTreePipeline(
        SyncedTree<TId> syncedTree,
        PropagationTree<TId> propagationTree)
    {
        _propagationTree = propagationTree;

        _missingAncestorsFactory = new MissingPropagationTreeAncestorsFactory<TId>(syncedTree, _propagationTree);
        _leavesRemovalOperationsFactory = new UnchangedPropagationTreeLeavesRemovalOperationsFactory<TId>();
        _equalizeOperationsFactory = new EqualizeOperationsFactory<PropagationTreeNodeModel<TId>, TId>(
            new PropagationTreeNodeModelMetadataEqualityComparer<TId>());
    }

    public void Execute(PropagationTreeNodeModel<TId> nodeModel)
    {
        Ensure.NotNull(nodeModel, nameof(nodeModel));

        var propagationNode = _propagationTree.NodeByIdOrDefault(nodeModel.Id);

        EqualizePropagationTreeNode(propagationNode, nodeModel);
    }

    private void EqualizePropagationTreeNode(
        PropagationTreeNode<TId>? node,
        PropagationTreeNodeModel<TId> sourceModel)
    {
        // Skip creating Unchanged node if parent does not exist or is Deleted
        if (sourceModel.RemoteStatus == UpdateStatus.Unchanged &&
            sourceModel.LocalStatus == UpdateStatus.Unchanged &&
            node == null)
        {
            var parent = _propagationTree.NodeByIdOrDefault(sourceModel.ParentId);
            if (parent == null)
            {
                // Parent does not exist in the PropagationTree
                return;
            }

            if (!parent.IsRoot &&
                (parent.Model.RemoteStatus.Contains(UpdateStatus.Deleted) ||
                 parent.Model.LocalStatus.Contains(UpdateStatus.Deleted)))
            {
                // Parent is deleted
                return;
            }
        }

        // In case different Created nodes were merged, two PropagationTree nodes might exist.
        // For now, while reconciliation first clears the PropagationTree and fills it with
        // the nodes from the Local Update Tree, only one node exists copied from the Remote
        // Update Tree.
        PropagationTreeNode<TId>? otherNode = null;

        if (!sourceModel.Id.Equals(sourceModel.AltId))
        {
            otherNode = _propagationTree.NodeByAltIdOrDefault(sourceModel.AltId);

            if (otherNode != null)
            {
                if (otherNode.Id.Equals(sourceModel.Id))
                {
                    otherNode = null;
                }
                else if (otherNode.Id.Equals(otherNode.AltId))
                {
                    // AltId must be unique in the tree, changing it on other node so that it do not conflict.
                    _propagationTree.Operations.Execute(new Operation<PropagationTreeNodeModel<TId>>(
                        OperationType.Update,
                        otherNode.Model.Copy()
                            .WithAltId(sourceModel.Id)));
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        var prevParent = node?.Parent;

        foreach (var operation in _equalizeOperationsFactory.Operations(node?.Model, sourceModel))
        {
            _propagationTree.Operations.Execute(WithMissingParents(operation));
        }

        RemoveUnchangedLeaves(node);

        RemoveUnchangedLeaves(prevParent);

        if (otherNode != null)
        {
            // Move all children of other node to new node.
            if (!otherNode.IsLeaf)
            {
                foreach (var otherChild in otherNode.Children)
                {
                    _propagationTree.Operations.Execute(new Operation<PropagationTreeNodeModel<TId>>(
                        OperationType.Move,
                        otherChild.Model.Copy()
                            .WithParentId(sourceModel.Id)));
                }
            }

            // Remove other node
            _propagationTree.Operations.Execute(new Operation<PropagationTreeNodeModel<TId>>(
                OperationType.Delete,
                otherNode.Model.Copy()));
        }
    }

    private void RemoveUnchangedLeaves(PropagationTreeNode<TId>? node)
    {
        _propagationTree.Operations.Execute(_leavesRemovalOperationsFactory.Operations(node));
    }

    private IEnumerable<Operation<PropagationTreeNodeModel<TId>>> WithMissingParents(
        Operation<PropagationTreeNodeModel<TId>> operation)
    {
        return _missingAncestorsFactory.WithMissingAncestors(operation);
    }
}
