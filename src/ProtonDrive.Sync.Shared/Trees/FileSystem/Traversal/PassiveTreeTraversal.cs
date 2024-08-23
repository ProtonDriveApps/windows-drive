using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem.Traversal;

public class PassiveTreeTraversal<TTree, TNode, TModel, TId>
    where TTree : FileSystemTree<TTree, TNode, TModel, TId>
    where TNode : FileSystemNode<TTree, TNode, TModel, TId>
    where TModel : FileSystemNodeModel<TId>, new()
    where TId : IEquatable<TId>
{
    private TNode? _startingNode;
    private bool _includeStartingNode = true;
    private bool _skipChildren;
    private bool _skipToParent;
    private bool _skipToRoot;

    /// <summary>
    /// Specifies to include the starting node into teh resulting sequence.
    /// By default the starting node is included into tre resulting sequence.
    /// </summary>
    /// <returns></returns>
    public PassiveTreeTraversal<TTree, TNode, TModel, TId> IncludeStartingNode()
    {
        _includeStartingNode = true;

        return this;
    }

    /// <summary>
    /// Specifies to exclude the starting node from the resulting sequence.
    /// By default the starting node is included into tre resulting sequence.
    /// </summary>
    /// <returns></returns>
    public PassiveTreeTraversal<TTree, TNode, TModel, TId> ExcludeStartingNode()
    {
        _includeStartingNode = false;

        return this;
    }

    /// <summary>
    /// Performs a sequentialisation of a tree into a sequence of tree nodes sorted by
    /// depth-first pre-order pattern.
    /// </summary>
    /// <param name="node">The node to start traversal from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A topologically sorted sequence of tree nodes.
    /// Parent nodes are returned before any of their child nodes.</returns>
    public IEnumerable<TNode> PreOrder(TNode node, CancellationToken cancellationToken = default)
    {
        return DepthFirst(node, TraversalOrder.PreOrder, cancellationToken);
    }

    /// <summary>
    /// Performs a sequentialisation of a tree into a sequence of tree nodes sorted by
    /// depth-first post-order pattern.
    /// </summary>
    /// <param name="node">The node to start traversal from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A sorted sequence of tree nodes.
    /// Parent nodes are returned after any of their child nodes.</returns>
    public IEnumerable<TNode> PostOrder(TNode node, CancellationToken cancellationToken)
    {
        return DepthFirst(node, TraversalOrder.PostOrder, cancellationToken);
    }

    /// <summary>
    /// Performs a sequentialisation of a tree into a sequence of tree nodes sorted by
    /// depth-first pattern.
    /// </summary>
    /// <param name="node">The node to start traversal from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A sorted sequence of value tuples containing the current tree node and
    /// sort order (either pre-order or post-order).</returns>
    /// <remarks>
    /// Each node is returned twice. Parent nodes are returned before any of their child
    /// nodes in a pre-order sequence and then parent nodes are returned after any of
    /// their child nodes in a post-order sequence.
    /// </remarks>
    public IEnumerable<(TNode Node, TraversalOrder Order)> DepthFirst(TNode node, CancellationToken cancellationToken = default)
    {
        _startingNode = node;

        return WithOptionallyIncludedStartingNode(DepthFirstInternal(node, cancellationToken));
    }

    /// <summary>
    /// When called before accessing the next item in the enumerator returned from <see cref="PreOrder"/>
    /// or <see cref="DepthFirst"/> method instructs to skip children of the current node.
    /// </summary>
    public void SkipChildren()
    {
        _skipChildren = true;
    }

    /// <summary>
    /// When called before accessing the next item in the enumerator returned from <see cref="PreOrder"/>,
    /// <see cref="PostOrder"/>, or <see cref="DepthFirst"/> method instructs to skip children and remaining
    /// siblings of the current node.
    /// </summary>
    public void SkipToParent()
    {
        _skipToParent = true;
    }

    /// <summary>
    /// When called before accessing the next item in the enumerator returned from <see cref="PreOrder"/>,
    /// <see cref="PostOrder"/>, or <see cref="DepthFirst"/> method instructs to skip children, remaining
    /// siblings of the current node, and remaining subtree up to the root. Continues to the next child of
    /// the root.
    /// </summary>
    public void SkipToRoot()
    {
        _skipToRoot = true;
    }

    private IEnumerable<(TNode Node, TraversalOrder Order)> WithOptionallyIncludedStartingNode(IEnumerable<(TNode Node, TraversalOrder Order)> items)
    {
        return items.Where(i => _includeStartingNode || i.Node != _startingNode);
    }

    private IEnumerable<TNode> DepthFirst(TNode node, TraversalOrder order, CancellationToken cancellationToken)
    {
        return DepthFirst(node, cancellationToken)
            .Where(i => i.Order == order)
            .Select(i => i.Node);
    }

    private IEnumerable<(TNode Node, TraversalOrder Order)> DepthFirstInternal(TNode node, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _skipChildren = false;
        _skipToParent = false;
        _skipToRoot = false;

        yield return (node, TraversalOrder.PreOrder);

        if (_skipToParent)
        {
            yield break;
        }

        if (node.IsDeleted)
        {
            yield break;
        }

        if (_skipToRoot && node.IsRoot)
        {
            _skipToRoot = false;
        }

        if (_skipToRoot)
        {
            yield break;
        }

        if (!_skipChildren && !node.IsLeaf)
        {
            foreach (var child in node.Children)
            {
                foreach (var item in DepthFirstInternal(child, cancellationToken))
                {
                    yield return item;
                }

                if (_skipToParent)
                {
                    break;
                }

                if (node.IsDeleted)
                {
                    yield break;
                }

                if (_skipToRoot && node.IsRoot)
                {
                    _skipToRoot = false;
                }

                if (_skipToRoot)
                {
                    yield break;
                }
            }

            _skipToParent = false;

            if (node.IsDeleted)
            {
                yield break;
            }
        }

        yield return (node, TraversalOrder.PostOrder);
    }
}
