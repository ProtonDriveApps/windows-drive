using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem.Traversal;

namespace Proton.Drive.Sdk.Sync.Adapter.Trees.Dirty;

internal static class DirtyTreeNodeExtensions
{
    private static IReadOnlyCollection<DirtyTreeNode<TId>> FromRootToNode<TId>(this DirtyTreeNode<TId> node)
        where TId : IEquatable<TId>
    {
        return node.FromRootToNode<DirtyTree<TId>, DirtyTreeNode<TId>, DirtyTreeNodeModel<TId>, TId>();
    }

    public static IEnumerable<DirtyTreeNode<TId>> FromParentToRoot<TId>(this DirtyTreeNode<TId> node)
        where TId : IEquatable<TId>
    {
        return node.FromParentToRoot<DirtyTree<TId>, DirtyTreeNode<TId>, DirtyTreeNodeModel<TId>, TId>();
    }

    public static IEnumerable<DirtyTreeNode<TId>> FromNodeToRoot<TId>(this DirtyTreeNode<TId> node)
        where TId : IEquatable<TId>
    {
        return node.FromNodeToRoot<DirtyTree<TId>, DirtyTreeNode<TId>, DirtyTreeNodeModel<TId>, TId>();
    }
}
