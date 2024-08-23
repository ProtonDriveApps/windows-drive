using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem.Traversal;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.Consolidation;

internal class DeletionOperationsFactory<TId>
    where TId : IEquatable<TId>
{
    private readonly Replica _replica;
    private readonly SyncedTree<TId> _syncedTree;

    private readonly PassiveTreeTraversal<UpdateTree<TId>, UpdateTreeNode<TId>, UpdateTreeNodeModel<TId>, TId> _treeTraversal = new();
    private readonly NearestUpdateTreeParent<TId> _nearestUpdateTreeParent;

    public DeletionOperationsFactory(
        Replica replica,
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> updateTree)
    {
        _replica = replica;
        _syncedTree = syncedTree;

        _nearestUpdateTreeParent = new NearestUpdateTreeParent<TId>(replica, updateTree);
    }

    public IEnumerable<Operation<UpdateTreeNodeModel<TId>>> Operations(
        UpdateTreeNode<TId> node)
    {
        return _treeTraversal
            .IncludeStartingNode()
            .PostOrder(node, CancellationToken.None)
            .SelectMany(PostOrderNodeDeletionOperations);
    }

    private IEnumerable<Operation<UpdateTreeNodeModel<TId>>> PostOrderNodeDeletionOperations(UpdateTreeNode<TId> node)
    {
        return DeleteChildrenOperations(node).Concat(DeletionOperations(node));
    }

    private IEnumerable<Operation<UpdateTreeNodeModel<TId>>> DeletionOperations(UpdateTreeNode<TId> node)
    {
        var model = node.Model;

        if (model.Status.Contains(UpdateStatus.Created))
        {
            // Created node gets deleted
            yield return DeleteOperation(model);
        }
        else if (model.Status.Contains(UpdateStatus.Moved))
        {
            var syncedNode = _syncedTree.NodeByOwnId(model.Id, _replica);
            var parentNode = _nearestUpdateTreeParent.NearestParent(node, syncedNode);

            if (parentNode?.Model.Status.Contains(UpdateStatus.Deleted) == true)
            {
                // Node moved from deleted original parent gets deleted
                yield return DeleteOperation(model);
            }
            else
            {
                // Node gets moved back to its original parent and gets Deleted status
                // If node also had Edited status, it doesn't get un-edited.
                yield return MoveAndSetDeletedStatusOperation(model, syncedNode);
            }
        }
        else if (model.Status.Contains(UpdateStatus.Deleted))
        {
            // Node with Deleted status does not require changes
        }
        else
        {
            // Node gets Deleted status
            // If node had Edited or Renamed status, it doesn't get un-edited or un-renamed.
            yield return SetDeletedStatusOperation(model);
        }
    }

    private IEnumerable<Operation<UpdateTreeNodeModel<TId>>> DeleteChildrenOperations(UpdateTreeNode<TId> node)
    {
        return node.IsLeaf
            ? Enumerable.Empty<Operation<UpdateTreeNodeModel<TId>>>()
            : node.Children.Select(child => DeleteOperation(child.Model));
    }

    private Operation<UpdateTreeNodeModel<TId>> SetDeletedStatusOperation(UpdateTreeNodeModel<TId> model)
    {
        return new(
            OperationType.Update,
            model.Copy().WithStatus(UpdateStatus.Deleted));
    }

    private Operation<UpdateTreeNodeModel<TId>> DeleteOperation(UpdateTreeNodeModel<TId> model)
    {
        return new(
            OperationType.Delete,
            new UpdateTreeNodeModel<TId> { Id = model.Id });
    }

    private Operation<UpdateTreeNodeModel<TId>> MoveAndSetDeletedStatusOperation(UpdateTreeNodeModel<TId> model, SyncedTreeNode<TId> linkSource)
    {
        return new(
            OperationType.Move,
            model.Copy()
                .WithName<UpdateTreeNodeModel<TId>, TId>(linkSource.Name)
                .WithParentId(linkSource.Parent!.Model.OwnId(_replica))
                .WithStatus(UpdateStatus.Deleted));
    }
}
