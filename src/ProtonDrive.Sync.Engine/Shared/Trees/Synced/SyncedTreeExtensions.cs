using System;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.Sync.Engine.Shared.Trees.Synced;

public static class SyncedTreeExtensions
{
    public static SyncedTreeNode<TId> NodeByOwnId<TId>(this SyncedTree<TId> tree, TId id, Replica replica)
        where TId : IEquatable<TId>
    {
        return tree.NodeByOwnIdOrDefault(id, replica) ??
               throw new TreeException($"Tree node with own Id={id} (Replica={replica}) does not exist");
    }

    public static SyncedTreeNode<TId>? NodeByOwnIdOrDefault<TId>(this SyncedTree<TId> tree, TId id, Replica replica)
        where TId : IEquatable<TId>
    {
        return replica == Replica.Remote ? tree.NodeByAltIdOrDefault(id) : tree.NodeByIdOrDefault(id);
    }

    public static SyncedTreeNode<TId>? NodeByOtherIdOrDefault<TId>(this SyncedTree<TId> tree, TId id, Replica replica)
        where TId : IEquatable<TId>
    {
        return replica == Replica.Local ? tree.NodeByAltIdOrDefault(id) : tree.NodeByIdOrDefault(id);
    }
}
