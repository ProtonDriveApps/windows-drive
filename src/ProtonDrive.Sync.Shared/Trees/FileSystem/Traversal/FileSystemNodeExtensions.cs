using System;
using System.Collections.Generic;
using System.Linq;
using ProtonDrive.Shared;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem.Traversal;

public static class FileSystemNodeExtensions
{
    public static IReadOnlyCollection<TNode> FromRootToNode<TTree, TNode, TModel, TId>(this TNode node)
        where TTree : FileSystemTree<TTree, TNode, TModel, TId>
        where TNode : FileSystemNode<TTree, TNode, TModel, TId>
        where TModel : FileSystemNodeModel<TId>, new()
        where TId : IEquatable<TId>
    {
        var stack = new Stack<TNode>();

        while (!node.IsRoot)
        {
            stack.Push(node);
            node = node.Parent!;
        }

        return stack;
    }

    public static TNode GetRootFolder<TTree, TNode, TModel, TId>(this TNode node)
        where TTree : FileSystemTree<TTree, TNode, TModel, TId>
        where TNode : FileSystemNode<TTree, TNode, TModel, TId>
        where TModel : FileSystemNodeModel<TId>, new()
        where TId : IEquatable<TId>
    {
        Ensure.IsFalse(node.IsRoot, "Node cannot be tree root", nameof(node));

        return FromNodeToRoot<TTree, TNode, TModel, TId>(node).Last();
    }

    public static IEnumerable<TNode> FromParentToRoot<TTree, TNode, TModel, TId>(this TNode node)
        where TTree : FileSystemTree<TTree, TNode, TModel, TId>
        where TNode : FileSystemNode<TTree, TNode, TModel, TId>
        where TModel : FileSystemNodeModel<TId>, new()
        where TId : IEquatable<TId>
    {
        return FromNodeToRoot<TTree, TNode, TModel, TId>(node).Skip(1);
    }

    public static IEnumerable<TNode> FromNodeToRoot<TTree, TNode, TModel, TId>(this TNode node)
        where TTree : FileSystemTree<TTree, TNode, TModel, TId>
        where TNode : FileSystemNode<TTree, TNode, TModel, TId>
        where TModel : FileSystemNodeModel<TId>, new()
        where TId : IEquatable<TId>
    {
        yield return node;

        while (!node.IsRoot)
        {
            node = node.Parent!;
            yield return node;
        }
    }
}
