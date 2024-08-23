using System;
using ProtonDrive.Sync.Shared;

namespace ProtonDrive.Sync.Engine.Shared.Trees.Propagation;

internal static class PropagationTreeNodeModelExtensions
{
    public static TId OwnId<TId>(this PropagationTreeNodeModel<TId> model, Replica replica)
        where TId : IEquatable<TId>
    {
        return model.OwnId<PropagationTreeNodeModel<TId>, TId>(replica);
    }

    public static TId OtherId<TId>(this PropagationTreeNodeModel<TId> model, Replica replica)
        where TId : IEquatable<TId>
    {
        return model.OtherId<PropagationTreeNodeModel<TId>, TId>(replica);
    }

    public static PropagationTreeNodeModel<TId> WithRemoteStatus<TId>(this PropagationTreeNodeModel<TId> model, UpdateStatus value)
        where TId : IEquatable<TId>
    {
        model.RemoteStatus = value;

        return model;
    }

    public static PropagationTreeNodeModel<TId> WithLocalStatus<TId>(this PropagationTreeNodeModel<TId> model, UpdateStatus value)
        where TId : IEquatable<TId>
    {
        model.LocalStatus = value;

        return model;
    }

    public static PropagationTreeNodeModel<TId> WithBackup<TId>(this PropagationTreeNodeModel<TId> model, bool value)
        where TId : IEquatable<TId>
    {
        model.Backup = value;

        return model;
    }

    public static PropagationTreeNodeModel<TId> WithOwnStatus<TId>(this PropagationTreeNodeModel<TId> model, UpdateStatus value, Replica replica)
        where TId : IEquatable<TId>
    {
        return replica == Replica.Remote
            ? model.WithRemoteStatus(value)
            : model.WithLocalStatus(value);
    }

    public static PropagationTreeNodeModel<TId> WithOtherStatus<TId>(this PropagationTreeNodeModel<TId> model, UpdateStatus value, Replica replica)
        where TId : IEquatable<TId>
    {
        return replica == Replica.Local
            ? model.WithRemoteStatus(value)
            : model.WithLocalStatus(value);
    }

    public static UpdateStatus OwnStatus<TId>(this PropagationTreeNodeModel<TId> model, Replica replica)
        where TId : IEquatable<TId>
    {
        return replica == Replica.Remote
            ? model.RemoteStatus
            : model.LocalStatus;
    }

    public static UpdateStatus OtherStatus<TId>(this PropagationTreeNodeModel<TId> model, Replica replica)
        where TId : IEquatable<TId>
    {
        return replica == Replica.Local
            ? model.RemoteStatus
            : model.LocalStatus;
    }
}
