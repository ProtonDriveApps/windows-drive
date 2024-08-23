using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Adapter.Trees.Dirty;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem.Traversal;

namespace ProtonDrive.Sync.Adapter.UpdateDetection.StateBased;

internal class DirtyNodesTraversal<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly DirtyTree<TId> _dirtyTree;

    private readonly PassiveTreeTraversal<DirtyTree<TId>, DirtyTreeNode<TId>, DirtyTreeNodeModel<TId>, TId> _dirtyTreeTraversal;

    private bool _dirtyTreeHasChanged;

    public DirtyNodesTraversal(AdapterTree<TId, TAltId> adapterTree, DirtyTree<TId> dirtyTree)
    {
        _adapterTree = adapterTree;
        _dirtyTree = dirtyTree;

        _dirtyTreeTraversal = new PassiveTreeTraversal<DirtyTree<TId>, DirtyTreeNode<TId>, DirtyTreeNodeModel<TId>, TId>();
        _dirtyTree.Operations.Executed += DirtyTree_Operations_Executed;
    }

    public IEnumerable<AdapterTreeNode<TId, TAltId>> DirtyNodes(TId startingNodeId, CancellationToken cancellationToken)
    {
        var startingNode = _dirtyTree.NodeByIdOrDefault(startingNodeId);
        if (startingNode == null)
        {
            return [];
        }

        return StableBranch(_dirtyTreeTraversal.IncludeStartingNode().DepthFirst(startingNode, cancellationToken))
            .WherePreOrder()
            .SelectNode()
            .Where(NodeAndChildrenShouldBeIncluded)
            .Where(node => node.Model.Status.HasAnyFlag(
                AdapterNodeStatus.DirtyPlaceholder |
                AdapterNodeStatus.DirtyAttributes |
                AdapterNodeStatus.DirtyChildren))
            .Select(AdapterTreeNode);
    }

    /// <summary>
    /// Produces the sequence of Adapter Tree nodes to be deleted, sorted in post order.
    /// Branches with dirty copied nodes are excluded.
    /// </summary>
    /// <remarks>
    /// The tree must not change while enumerating.
    /// </remarks>
    /// <param name="startingNodeId">The identity of the node to start tree traversal.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The sequence of Adapter Tree nodes to be deleted, sorted in post order.</returns>
    public IEnumerable<AdapterTreeNode<TId, TAltId>> LostOrDeletedNodes(TId startingNodeId, CancellationToken cancellationToken)
    {
        var startingNode = _dirtyTree.NodeByIdOrDefault(startingNodeId);
        if (startingNode == null)
        {
            return [];
        }

        return DeletableBranchesInPostOrder(_dirtyTreeTraversal.IncludeStartingNode().DepthFirst(startingNode, cancellationToken))
            .Where(node => node.Model.IsLostOrDeleted())
            .Select(AdapterTreeNode);
    }

    private IEnumerable<DirtyTreeNode<TId>> DeletableBranchesInPostOrder(IEnumerable<(DirtyTreeNode<TId> Node, TraversalOrder Order)> origin)
    {
        var verdicts = new Stack<bool>();
        var canDelete = true;
        var isTraversingUp = false;

        foreach (var (node, order) in origin)
        {
            switch (order)
            {
                case TraversalOrder.PreOrder:
                    if (isTraversingUp)
                    {
                        var previousVerdict = verdicts.Pop();
                        verdicts.Push(previousVerdict && canDelete);
                        canDelete = true;
                    }

                    isTraversingUp = false;

                    canDelete &= !node.Model.Status.HasFlag(AdapterNodeStatus.DirtyCopiedFrom);
                    verdicts.Push(canDelete);
                    canDelete = true;

                    break;

                case TraversalOrder.PostOrder:
                    isTraversingUp = true;

                    canDelete &= verdicts.Pop();

                    if (canDelete)
                    {
                        yield return node;
                    }

                    break;

                default:
                    throw new InvalidEnumArgumentException(nameof(order), (int)order, typeof(TraversalOrder));
            }
        }
    }

    private IEnumerable<(DirtyTreeNode<TId> Node, TraversalOrder Order)> StableBranch(IEnumerable<(DirtyTreeNode<TId> Node, TraversalOrder Order)> origin)
    {
        foreach (var item in origin)
        {
            if (SkipUnstableBranch(item.Node))
            {
                continue;
            }

            yield return item;

            if (item.Node.IsDeleted)
            {
                continue;
            }

            SkipUnstableChildren(item.Node);
            SkipUnstableBranch(item.Node);
        }
    }

    private bool SkipUnstableBranch(DirtyTreeNode<TId> node)
    {
        if (_dirtyTreeHasChanged && !BranchIsStable(node))
        {
            _dirtyTreeTraversal.SkipToParent();

            return true;
        }

        _dirtyTreeHasChanged = false;

        return false;
    }

    private void SkipUnstableChildren(DirtyTreeNode<TId> node)
    {
        if (!StartsStableBranch(node))
        {
            _dirtyTreeTraversal.SkipChildren();
        }
    }

    private bool BranchIsStable(DirtyTreeNode<TId> node)
    {
        return node.IsRoot || node.FromParentToRoot().All(StartsStableBranch);
    }

    private bool StartsStableBranch(DirtyTreeNode<TId> node)
    {
        return !node.Model.HasDirtyDescendantsFlag() &&
               !node.Model.IsLostOrDeleted() &&
               !node.Model.IsDirtyPlaceholder();
    }

    private bool NodeAndChildrenShouldBeIncluded(DirtyTreeNode<TId> node)
    {
        if (node.Model.IsLostOrDeleted())
        {
            _dirtyTreeTraversal.SkipChildren();
            return false;
        }

        return true;
    }

    private AdapterTreeNode<TId, TAltId> AdapterTreeNode(DirtyTreeNode<TId> node)
    {
        return _adapterTree.NodeByIdOrDefault(node.Id) ??
               throw new TreeException($"AdapterTree node with Id={node.Id} does not exist");
    }

    private void DirtyTree_Operations_Executed(object? sender, FileSystemTreeOperationExecutedEventArgs<DirtyTreeNodeModel<TId>, TId> e)
    {
        _dirtyTreeHasChanged = true;
    }
}
