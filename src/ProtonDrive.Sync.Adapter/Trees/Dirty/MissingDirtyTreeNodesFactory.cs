using System;
using System.Collections.Generic;
using System.Linq;
using ProtonDrive.Shared.Linq;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.Trees.Dirty;

internal class MissingDirtyTreeNodesFactory<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly DirtyTree<TId> _dirtyTree;

    public MissingDirtyTreeNodesFactory(
        AdapterTree<TId, TAltId> adapterTree,
        DirtyTree<TId> dirtyTree)
    {
        _adapterTree = adapterTree;
        _dirtyTree = dirtyTree;
    }

    public IEnumerable<Operation<DirtyTreeNodeModel<TId>>> WithMissingParents(
        IEnumerable<Operation<DirtyTreeNodeModel<TId>>> operations)
    {
        return operations.SelectMany(WithMissingParents);
    }

    public IEnumerable<Operation<DirtyTreeNodeModel<TId>>> WithMissingParents(
        Operation<DirtyTreeNodeModel<TId>> operation)
    {
        if (operation.Type is OperationType.Create or OperationType.Move)
        {
            return Operations(operation.Model.ParentId)
                .Append(operation);
        }

        return operation.Yield();
    }

    private IEnumerable<Operation<DirtyTreeNodeModel<TId>>> Operations(TId nodeId)
    {
        var node = _dirtyTree.NodeByIdOrDefault(nodeId);
        if (node != null)
        {
            return Enumerable.Empty<Operation<DirtyTreeNodeModel<TId>>>();
        }

        var sourceNode = _adapterTree.NodeByIdOrDefault(nodeId);
        if (sourceNode == null)
        {
            throw new InvalidOperationException($"Adapter Tree node with Id={nodeId} does not exist");
        }

        return CopiedFromSourceTree(sourceNode);
    }

    private IEnumerable<Operation<DirtyTreeNodeModel<TId>>> CopiedFromSourceTree(
        AdapterTreeNode<TId, TAltId> sourceNode)
    {
        var stack = new Stack<AdapterTreeNode<TId, TAltId>>();
        DirtyTreeNode<TId>? node = null;

        while (node == null && !sourceNode.IsRoot)
        {
            stack.Push(sourceNode);

            sourceNode = sourceNode.Parent!;
            node = _dirtyTree.NodeByIdOrDefault(sourceNode.Id);
        }

        while (stack.Count != 0)
        {
            sourceNode = stack.Pop();
            var model = sourceNode.Model.ToDirtyTreeNodeModel();

            yield return new Operation<DirtyTreeNodeModel<TId>>(OperationType.Create, model);
        }
    }
}
