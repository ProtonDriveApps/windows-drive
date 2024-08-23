using System;
using System.Collections.Generic;
using System.Linq;
using ProtonDrive.Shared.Linq;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.ConflictResolution;

internal class MissingPropagationTreeAncestorsFactory<TId>
    where TId : IEquatable<TId>
{
    private readonly SyncedTree<TId> _syncedTree;
    private readonly PropagationTree<TId> _propagationTree;

    public MissingPropagationTreeAncestorsFactory(
        SyncedTree<TId> syncedTree,
        PropagationTree<TId> propagationTree)
    {
        _syncedTree = syncedTree;
        _propagationTree = propagationTree;
    }

    public IEnumerable<Operation<PropagationTreeNodeModel<TId>>> WithMissingAncestors(
        IEnumerable<Operation<PropagationTreeNodeModel<TId>>> operations)
    {
        return operations.SelectMany(WithMissingAncestors);
    }

    public IEnumerable<Operation<PropagationTreeNodeModel<TId>>> WithMissingAncestors(
        Operation<PropagationTreeNodeModel<TId>> operation)
    {
        if (operation.Type == OperationType.Create || operation.Type == OperationType.Move)
        {
            return CreateAncestorsOperations(operation.Model.ParentId)
                .Append(operation);
        }

        return operation.Yield();
    }

    private IEnumerable<Operation<PropagationTreeNodeModel<TId>>> CreateAncestorsOperations(TId nodeId)
    {
        var node = _propagationTree.NodeByIdOrDefault(nodeId);
        if (node != null)
        {
            // Node already exists, no need to create ancestors
            return Enumerable.Empty<Operation<PropagationTreeNodeModel<TId>>>();
        }

        var syncedNode = _syncedTree.NodeByIdOrDefault(nodeId);
        if (syncedNode == null)
        {
            throw new InvalidOperationException($"SyncedTree node Id={nodeId} does not exist");
        }

        return CopiedFromSyncedTree(syncedNode);
    }

    private IEnumerable<Operation<PropagationTreeNodeModel<TId>>> CopiedFromSyncedTree(
        SyncedTreeNode<TId> syncedNode)
    {
        // Missing ancestors are created by copying them from the SyncedTree with Unchanged status values

        var stack = new Stack<SyncedTreeNode<TId>>();
        PropagationTreeNode<TId>? node = null;

        while (node == null && !syncedNode.IsRoot)
        {
            stack.Push(syncedNode);

            syncedNode = syncedNode.Parent!;
            node = _propagationTree.NodeByIdOrDefault(syncedNode.Id);
        }

        if (node != null && (node.Model.RemoteStatus.Contains(UpdateStatus.Deleted) || node.Model.LocalStatus.Contains(UpdateStatus.Deleted)))
        {
            throw new InvalidOperationException($"Parent node Id={node.Id} status is Deleted");
        }

        while (stack.Count != 0)
        {
            syncedNode = stack.Pop();
            var model = new PropagationTreeNodeModel<TId>()
                .CopiedFrom(syncedNode.Model)
                .WithAltId(syncedNode.Model.AltId)
                .WithRemoteStatus(UpdateStatus.Unchanged)
                .WithLocalStatus(UpdateStatus.Unchanged);

            yield return new Operation<PropagationTreeNodeModel<TId>>(OperationType.Create, model);
        }
    }
}
