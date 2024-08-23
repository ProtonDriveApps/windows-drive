using System;
using ProtonDrive.Sync.Shared;

namespace ProtonDrive.Sync.Engine.Shared.Trees.Synced;

public static class SyncedTreeNodeModelExtensions
{
    public static TId OwnId<TId>(this SyncedTreeNodeModel<TId> model, Replica replica)
        where TId : IEquatable<TId>
    {
        return model.OwnId<SyncedTreeNodeModel<TId>, TId>(replica);
    }

    public static TId OtherId<TId>(this SyncedTreeNodeModel<TId> model, Replica replica)
        where TId : IEquatable<TId>
    {
        return model.OtherId<SyncedTreeNodeModel<TId>, TId>(replica);
    }
}
