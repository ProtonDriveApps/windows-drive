using System;
using System.Collections.Generic;
using System.Linq;
using ProtonDrive.Shared.Linq;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.Trees.StateMaintenance;

internal class MissingStateMaintenanceTreeNodesFactory<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly StateMaintenanceTree<TId> _stateMaintenanceTree;

    public MissingStateMaintenanceTreeNodesFactory(
        AdapterTree<TId, TAltId> adapterTree,
        StateMaintenanceTree<TId> stateMaintenanceTree)
    {
        _adapterTree = adapterTree;
        _stateMaintenanceTree = stateMaintenanceTree;
    }

    public IEnumerable<Operation<StateMaintenanceTreeNodeModel<TId>>> WithMissingAncestors(
        IEnumerable<Operation<StateMaintenanceTreeNodeModel<TId>>> operations)
    {
        return operations.SelectMany(WithMissingAncestors);
    }

    public IEnumerable<Operation<StateMaintenanceTreeNodeModel<TId>>> WithMissingAncestors(
        Operation<StateMaintenanceTreeNodeModel<TId>> operation)
    {
        if (operation.Type is OperationType.Create or OperationType.Move)
        {
            return Operations(operation.Model.ParentId)
                .Append(operation);
        }

        return operation.Yield();
    }

    private IEnumerable<Operation<StateMaintenanceTreeNodeModel<TId>>> Operations(TId nodeId)
    {
        var node = _stateMaintenanceTree.NodeByIdOrDefault(nodeId);
        if (node != null)
        {
            return Enumerable.Empty<Operation<StateMaintenanceTreeNodeModel<TId>>>();
        }

        var sourceNode = _adapterTree.NodeByIdOrDefault(nodeId);
        if (sourceNode == null)
        {
            throw new InvalidOperationException($"State Maintenance Tree node with Id={nodeId} does not exist");
        }

        return CopiedFromSourceTree(sourceNode);
    }

    private IEnumerable<Operation<StateMaintenanceTreeNodeModel<TId>>> CopiedFromSourceTree(
        AdapterTreeNode<TId, TAltId> sourceNode)
    {
        var stack = new Stack<AdapterTreeNode<TId, TAltId>>();
        StateMaintenanceTreeNode<TId>? node = null;

        while (node == null && !sourceNode.IsRoot)
        {
            stack.Push(sourceNode);

            sourceNode = sourceNode.Parent!;
            node = _stateMaintenanceTree.NodeByIdOrDefault(sourceNode.Id);
        }

        while (stack.Count != 0)
        {
            sourceNode = stack.Pop();
            var model = sourceNode.Model.ToStateMaintenanceTreeNodeModel();

            yield return new Operation<StateMaintenanceTreeNodeModel<TId>>(OperationType.Create, model);
        }
    }
}
