using System;
using System.IO;
using System.Linq;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.Trees.Adapter.NodeLookup;

internal static class AdapterTreeExtensions
{
    public static AdapterTreeNode<TId, TAltId>? NodeByPath<TId, TAltId>(this AdapterTree<TId, TAltId> tree, string path)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        var node = tree.Root;
        foreach (var name in Split(path))
        {
            node = node.ChildrenByName(name).SingleOrDefault(n => n.Name == name && !n.Model.IsLostOrDeleted());
            if (node == null)
            {
                break;
            }
        }

        return node;
    }

    public static NodeByPathLookupResult<TId, TAltId> NodeOrNearestAncestorByPath<TId, TAltId>(this AdapterTree<TId, TAltId> tree, string path)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        var node = tree.Root;
        var names = Split(path);
        for (var i = 0; i < names.Length; i++)
        {
            var name = names[i];
            var childNode = node.Type == NodeType.Directory
                ? node.ChildrenByName(name).SingleOrDefault(n => n.Name == name && !n.Model.IsLostOrDeleted())
                : null;

            if (childNode == null)
            {
                return i == names.Length - 1
                    // The parent node found by path
                    ? NodeByPathLookupResult<TId, TAltId>.ParentFound(node, name)
                    // The nearest ancestor node found by path except the parent
                    : NodeByPathLookupResult<TId, TAltId>.AncestorFound(node, name);
            }

            node = childNode;
        }

        // The node found by path
        return NodeByPathLookupResult<TId, TAltId>.NodeFound(node);
    }

    private static string[] Split(string path)
    {
        return path.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
    }
}
