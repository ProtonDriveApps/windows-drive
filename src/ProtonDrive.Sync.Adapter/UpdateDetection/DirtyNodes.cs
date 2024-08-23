using System;
using System.Collections.Generic;
using System.Linq;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Adapter.Trees.Dirty;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.UpdateDetection;

/// <summary>
/// Listens for the changes to the AdapterTree and updates the DirtyTree accordingly.
/// The DirtyTree contains only nodes with dirty flags set and ancestor nodes connecting them
/// with the root.
/// </summary>
/// <typeparam name="TId">Type of the node identity property.</typeparam>
/// <typeparam name="TAltId">Type of the alternative identity property.</typeparam>
internal class DirtyNodes<TId, TAltId> : IDirtyNodes<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly DirtyTree<TId> _dirtyTree;

    private readonly MissingDirtyTreeNodesFactory<TId, TAltId> _missingNodesFactory;
    private readonly DirtyTreeLeavesRemovalOperationsFactory<TId> _leavesRemoval;

    public DirtyNodes(AdapterTree<TId, TAltId> adapterTree, DirtyTree<TId> dirtyTree)
    {
        _dirtyTree = dirtyTree;

        _missingNodesFactory = new MissingDirtyTreeNodesFactory<TId, TAltId>(adapterTree, dirtyTree);
        _leavesRemoval = new DirtyTreeLeavesRemovalOperationsFactory<TId>();

        adapterTree.Operations.Executed += OnAdapterTreeOperationExecuted;
    }

    public bool IsEmpty { get; private set; }

    public void Initialize()
    {
        IsEmpty = _dirtyTree.Root.IsLeaf;
    }

    public bool BranchIsDirty(AdapterTreeNode<TId, TAltId> node)
    {
        return
            /* The node flagged DirtyPlaceholder cannot contain children, it is a root of dirty branch.*/
            node.Model.IsDirtyPlaceholder() ||
            /* The ancestor flagged DirtyDescendants is a root of dirty branch.*/
            node.FromParentToRoot().Any(parent => parent.Model.HasDirtyDescendantsFlag());
    }

    private void OnAdapterTreeOperationExecuted(object? sender, FileSystemTreeOperationExecutedEventArgs<AdapterTreeNodeModel<TId, TAltId>, TId> eventArgs)
    {
        var node = eventArgs.Type != OperationType.Create ? _dirtyTree.NodeByIdOrDefault(eventArgs.OldModel!.Id) : null;

        if (node != null)
        {
            var prevParent = node.Parent;

            Execute(
                WithMissingParents(
                    new Operation<DirtyTreeNodeModel<TId>>(
                        eventArgs.Type,
                        (eventArgs.NewModel ?? eventArgs.OldModel ?? throw new InvalidOperationException()).ToDirtyTreeNodeModel())));

            RemoveUnneededLeaves(node);
            RemoveUnneededLeaves(prevParent);

            IsEmpty = eventArgs.NewModel?.IsCandidateForDirtyTree() != true && _dirtyTree.Root.IsLeaf;
        }
        else
        {
            var shouldBeOnTree = eventArgs.NewModel?.IsCandidateForDirtyTree() == true;
            if (shouldBeOnTree)
            {
                Execute(
                    WithMissingParents(
                        new Operation<DirtyTreeNodeModel<TId>>(
                            OperationType.Create,
                            eventArgs.NewModel!.ToDirtyTreeNodeModel())));

                IsEmpty = false;
            }
        }
    }

    private void RemoveUnneededLeaves(DirtyTreeNode<TId>? node)
    {
        Execute(_leavesRemoval.Operations(node));
    }

    private IEnumerable<Operation<DirtyTreeNodeModel<TId>>> WithMissingParents(
        Operation<DirtyTreeNodeModel<TId>> operation)
    {
        return _missingNodesFactory.WithMissingParents(operation);
    }

    private void Execute(IEnumerable<Operation<DirtyTreeNodeModel<TId>>> operations)
    {
        _dirtyTree.Operations.Execute(operations);
    }
}
