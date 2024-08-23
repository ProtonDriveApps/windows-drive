using System;
using System.Collections.Generic;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Adapter.Trees.StateMaintenance;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.SyncStateMaintenance;

/// <summary>
/// Listens for the changes to the Adapter Tree and updates the State Maintenance Tree accordingly.
/// </summary>
/// <remarks>
/// The State Maintenance Tree contains only nodes that are candidate for file sync state update
/// on the filesystem. Those are nodes with both <see cref="AdapterNodeStatus.Synced"/>
/// and <see cref="AdapterNodeStatus.StateUpdatePending"/> flags set, and ancestor nodes
/// connecting them with the root.
/// </remarks>
/// <typeparam name="TId">Type of the node identity property.</typeparam>
/// <typeparam name="TAltId">Type of the alternative identity property.</typeparam>
internal class StateMaintenanceTreeHandler<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly StateMaintenanceTree<TId> _stateMaintenanceTree;

    private readonly MissingStateMaintenanceTreeNodesFactory<TId, TAltId> _missingNodesFactory;
    private readonly StateMaintenanceTreeLeavesRemovalOperationsFactory<TId> _leavesRemoval;

    public StateMaintenanceTreeHandler(AdapterTree<TId, TAltId> adapterTree, StateMaintenanceTree<TId> stateMaintenanceTree)
    {
        _stateMaintenanceTree = stateMaintenanceTree;

        _missingNodesFactory = new MissingStateMaintenanceTreeNodesFactory<TId, TAltId>(adapterTree, stateMaintenanceTree);
        _leavesRemoval = new StateMaintenanceTreeLeavesRemovalOperationsFactory<TId>();

        adapterTree.Operations.Executed += OnAdapterTreeOperationExecuted;
    }

    public bool IsEmpty { get; private set; }

    public void Initialize()
    {
        IsEmpty = _stateMaintenanceTree.Root.IsLeaf;
    }

    private void OnAdapterTreeOperationExecuted(object? sender, FileSystemTreeOperationExecutedEventArgs<AdapterTreeNodeModel<TId, TAltId>, TId> eventArgs)
    {
        var node = eventArgs.Type != OperationType.Create ? _stateMaintenanceTree.NodeByIdOrDefault(eventArgs.OldModel!.Id) : null;

        if (node != null)
        {
            var prevParent = node.Parent;

            Execute(
                WithMissingParents(
                    new Operation<StateMaintenanceTreeNodeModel<TId>>(
                        eventArgs.Type,
                        (eventArgs.NewModel ?? eventArgs.OldModel ?? throw new InvalidOperationException()).ToStateMaintenanceTreeNodeModel())));

            RemoveUnneededLeaves(node);
            RemoveUnneededLeaves(prevParent);

            IsEmpty = eventArgs.NewModel?.IsCandidateForSyncStateUpdate() != true && _stateMaintenanceTree.Root.IsLeaf;
        }
        else
        {
            var shouldBeOnTree = eventArgs.NewModel?.IsCandidateForSyncStateUpdate() == true;
            if (shouldBeOnTree)
            {
                Execute(
                    WithMissingParents(
                        new Operation<StateMaintenanceTreeNodeModel<TId>>(
                            OperationType.Create,
                            eventArgs.NewModel!.ToStateMaintenanceTreeNodeModel())));

                IsEmpty = false;
            }
        }
    }

    private void RemoveUnneededLeaves(StateMaintenanceTreeNode<TId>? node)
    {
        Execute(_leavesRemoval.Operations(node));
    }

    private IEnumerable<Operation<StateMaintenanceTreeNodeModel<TId>>> WithMissingParents(
        Operation<StateMaintenanceTreeNodeModel<TId>> operation)
    {
        return _missingNodesFactory.WithMissingAncestors(operation);
    }

    private void Execute(IEnumerable<Operation<StateMaintenanceTreeNodeModel<TId>>> operations)
    {
        _stateMaintenanceTree.Operations.Execute(operations);
    }
}
