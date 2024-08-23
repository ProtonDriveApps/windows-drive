using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ProtonDrive.Sync.Shared.Trees.FileSystem.Traversal;

public static class EnumerableTreeTraversalExtensions
{
    public static IEnumerable<(TNode Node, TraversalOrder Order)> PreOrder<TNode>(
        this IEnumerable<(TNode Node, TraversalOrder Order)> source,
        Action<TNode> action)
    {
        return PreOrPostOrder(source, action, TraversalOrder.PreOrder);
    }

    public static IEnumerable<(TNode Node, TraversalOrder Order)> PostOrder<TNode>(
        this IEnumerable<(TNode Node, TraversalOrder Order)> source,
        Action<TNode> action)
    {
        return PreOrPostOrder(source, action, TraversalOrder.PostOrder);
    }

    public static IEnumerable<(TNode Node, TraversalOrder Order)> WherePreOrder<TNode>(
        this IEnumerable<(TNode Node, TraversalOrder Order)> source)
    {
        return source.Where(i => i.Order == TraversalOrder.PreOrder);
    }

    public static IEnumerable<(TNode Node, TraversalOrder Order)> WherePostOrder<TNode>(
        this IEnumerable<(TNode Node, TraversalOrder Order)> source)
    {
        return source.Where(i => i.Order == TraversalOrder.PostOrder);
    }

    public static IEnumerable<TNode> SelectNode<TNode>(
        this IEnumerable<(TNode Node, TraversalOrder Order)> source)
    {
        return source.Select(i => i.Node);
    }

    public static void Execute<TNode>(
        this IEnumerable<(TNode Node, TraversalOrder Order)> source)
    {
        using var enumerator = source.GetEnumerator();
        while (enumerator.MoveNext())
        {
        }
    }

    private static IEnumerable<(TNode Node, TraversalOrder Order)> PreOrPostOrder<TNode>(
        IEnumerable<(TNode Node, TraversalOrder Order)> source,
        Action<TNode> action,
        TraversalOrder order)
    {
        foreach (var item in source)
        {
            if (item.Order == order)
            {
                action(item.Node);
            }

            yield return item;
        }
    }
}
