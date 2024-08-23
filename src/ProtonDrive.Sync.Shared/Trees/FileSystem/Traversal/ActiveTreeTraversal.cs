using System;
using System.Collections.Generic;
using System.Linq;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem.Traversal;

public class ActiveTreeTraversal<TTree, TNode, TModel, TId>
    where TTree : FileSystemTree<TTree, TNode, TModel, TId>
    where TNode : FileSystemNode<TTree, TNode, TModel, TId>
    where TModel : FileSystemNodeModel<TId>, new()
    where TId : IEquatable<TId>
{
    private Action<TNode>? _preOrderAction;
    private Action<TNode>? _postOrderAction;
    private Func<TNode, IEnumerable<TNode>>? _childrenFunction;
    private TNode? _startingNode;
    private bool _includeStartingNode = true;
    private bool _skipChildren;

    public ActiveTreeTraversal<TTree, TNode, TModel, TId> PreOrder(Action<TNode> action)
    {
        _preOrderAction = action;

        return this;
    }

    public ActiveTreeTraversal<TTree, TNode, TModel, TId> PostOrder(Action<TNode> action)
    {
        _postOrderAction = action;

        return this;
    }

    public ActiveTreeTraversal<TTree, TNode, TModel, TId> Children(Func<TNode, IEnumerable<TNode>> function)
    {
        _childrenFunction = function;

        return this;
    }

    public ActiveTreeTraversal<TTree, TNode, TModel, TId> IncludeStartingNode()
    {
        _includeStartingNode = true;

        return this;
    }

    public ActiveTreeTraversal<TTree, TNode, TModel, TId> ExcludeStartingNode()
    {
        _includeStartingNode = false;

        return this;
    }

    public void Execute(TNode startingNode)
    {
        _startingNode = startingNode;

        TraverseDepthFirst(startingNode);

        _startingNode = null;
    }

    /// <summary>
    /// When called from the callback action passed to the <see cref="PreOrder"/> method
    /// instructs to skip children of the current node.
    /// </summary>
    /// <param name="value">True to skip children; False otherwise. Default is True.</param>
    public void SkipChildren(bool value = true)
    {
        _skipChildren = value;
    }

    private void TraverseDepthFirst(TNode node)
    {
        PreOrder(node);

        foreach (var child in Children(node))
        {
            TraverseDepthFirst(child);

            if (node.IsDeleted)
            {
                return;
            }
        }

        PostOrder(node);
    }

    private void PreOrder(TNode node)
    {
        Execute(_preOrderAction, node);
    }

    private void PostOrder(TNode node)
    {
        Execute(_postOrderAction, node);
    }

    private IEnumerable<TNode> Children(TNode node)
    {
        if (node.IsLeaf || _skipChildren)
        {
            return Enumerable.Empty<TNode>();
        }

        if (_childrenFunction == null)
        {
            return node.Children;
        }

        return _childrenFunction(node);
    }

    private void Execute(Action<TNode>? action, TNode node)
    {
        _skipChildren = false;

        if (action == null)
        {
            return;
        }

        if (node == _startingNode && !_includeStartingNode)
        {
            return;
        }

        action(node);
    }
}
