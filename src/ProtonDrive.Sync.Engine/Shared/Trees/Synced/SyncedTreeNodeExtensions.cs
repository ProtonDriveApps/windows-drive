using System;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.Shared.Trees.Synced;

public static class SyncedTreeNodeExtensions
{
    internal static bool IsSyncRoot<TId>(this SyncedTreeNode<TId> node)
        where TId : IEquatable<TId>
    {
        // First level folders are sync roots
        return node.Parent?.IsRoot == true && node.Type == NodeType.Directory;
    }
}
