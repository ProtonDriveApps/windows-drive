using System;
using System.Collections.Generic;
using System.Linq;
using ProtonDrive.Shared.Linq;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.Consolidation;

internal class MissingUpdateTreeAncestorsFactory<TId>
    where TId : IEquatable<TId>
{
    private readonly Replica _replica;
    private readonly SyncedTree<TId> _syncedTree;
    private readonly UpdateTree<TId> _updateTree;

    public MissingUpdateTreeAncestorsFactory(
        Replica replica,
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> updateTree)
    {
        _replica = replica;
        _syncedTree = syncedTree;
        _updateTree = updateTree;
    }

    public IEnumerable<Operation<UpdateTreeNodeModel<TId>>> WithMissingAncestors(
        IEnumerable<Operation<UpdateTreeNodeModel<TId>>> operations)
    {
        return operations.SelectMany(WithMissingAncestors);
    }

    public IEnumerable<Operation<UpdateTreeNodeModel<TId>>> WithMissingAncestors(
        Operation<UpdateTreeNodeModel<TId>> operation)
    {
        if (operation.Type == OperationType.Create || operation.Type == OperationType.Move)
        {
            return Operations(operation.Model.ParentId)
                .Append(operation);
        }

        return operation.Yield();
    }

    private IEnumerable<Operation<UpdateTreeNodeModel<TId>>> Operations(TId nodeId)
    {
        var node = _updateTree.NodeByIdOrDefault(nodeId);
        if (node != null)
        {
            if (node.Model.Status.Contains(UpdateStatus.Deleted))
            {
                throw new InvalidOperationException($"UpdateTree node with Id={node.Id} status is Deleted");
            }

            return Enumerable.Empty<Operation<UpdateTreeNodeModel<TId>>>();
        }

        var syncedNode = _syncedTree.NodeByOwnId(nodeId, _replica);

        return CopiedFromSyncedTree(syncedNode);
    }

    private IEnumerable<Operation<UpdateTreeNodeModel<TId>>> CopiedFromSyncedTree(
        SyncedTreeNode<TId> syncedNode)
    {
        var stack = new Stack<SyncedTreeNode<TId>>();
        UpdateTreeNode<TId>? node = null;

        while (node == null && !syncedNode.IsRoot)
        {
            stack.Push(syncedNode);

            syncedNode = syncedNode.Parent!;
            node = _updateTree.NodeByIdOrDefault(syncedNode.Model.OwnId(_replica));
        }

        if (node?.Model.Status.Contains(UpdateStatus.Deleted) == true)
        {
            throw new InvalidOperationException($"UpdateTree parent node with Id={node.Id} status is Deleted");
        }

        while (stack.Count != 0)
        {
            syncedNode = stack.Pop();
            var model = new UpdateTreeNodeModel<TId>()
                .CopiedFrom(syncedNode.Model)
                .WithStatus(UpdateStatus.Unchanged)
                .WithId(syncedNode.Model.OwnId(_replica))
                .WithParentId(syncedNode.Parent!.Model.OwnId(_replica));

            yield return new Operation<UpdateTreeNodeModel<TId>>(OperationType.Create, model);
        }
    }
}
