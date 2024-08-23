using System;
using ProtonDrive.Sync.Shared;

namespace ProtonDrive.Sync.Engine.Shared.Trees.Propagation;

internal static class PropagationTreeExtensions
{
    public static PropagationTreeNode<TId>? NodeByOwnIdOrDefault<TId>(this PropagationTree<TId> tree, TId id, Replica replica)
        where TId : IEquatable<TId>
    {
        return replica == Replica.Remote ? tree.NodeByAltIdOrDefault(id) : tree.NodeByIdOrDefault(id);
    }

    public static PropagationTreeNode<TId>? NodeByOtherIdOrDefault<TId>(this PropagationTree<TId> tree, TId id, Replica replica)
        where TId : IEquatable<TId>
    {
        return replica == Replica.Local ? tree.NodeByAltIdOrDefault(id) : tree.NodeByIdOrDefault(id);
    }
}
