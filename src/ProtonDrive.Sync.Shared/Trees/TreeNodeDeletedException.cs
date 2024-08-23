using System;
using ProtonDrive.Shared;

namespace ProtonDrive.Sync.Shared.Trees;

public class TreeNodeDeletedException : TreeException
{
    public TreeNodeDeletedException()
    {
    }

    public TreeNodeDeletedException(string message)
        : base(message)
    {
    }

    public static TreeNodeDeletedException FromNode<TId>(IIdentifiable<TId> node)
        where TId : IEquatable<TId>
    {
        return new TreeNodeDeletedException($"Node Id={node.Id} has been deleted from the tree");
    }

    public TreeNodeDeletedException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
