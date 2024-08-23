using System;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.Sync.Adapter.Trees.Adapter;

public static class AdapterTreeNodeModelExtensions
{
    public static AdapterTreeNodeModel<TId, TAltId> WithDirtyFlags<TId, TAltId>(
        this AdapterTreeNodeModel<TId, TAltId> nodeModel, AdapterNodeStatus value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return nodeModel
            .WithStatus((nodeModel.Status & ~AdapterNodeStatus.DirtyMask) | value);
    }

    public static AdapterTreeNodeModel<TId, TAltId> WithAppendedFlags<TId, TAltId>(
        this AdapterTreeNodeModel<TId, TAltId> nodeModel, AdapterNodeStatus value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return nodeModel
            .WithStatus(nodeModel.Status | value);
    }

    public static AdapterTreeNodeModel<TId, TAltId> WithRemovedFlags<TId, TAltId>(
        this AdapterTreeNodeModel<TId, TAltId> nodeModel, AdapterNodeStatus value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return nodeModel
            .WithStatus(nodeModel.Status & ~value);
    }

    public static bool IsSynced<TId, TAltId>(
        this AdapterTreeNodeModel<TId, TAltId> nodeModel)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return nodeModel.Status.HasFlag(AdapterNodeStatus.Synced);
    }

    public static AdapterTreeNodeModel<TId, TAltId> WithSyncedFlag<TId, TAltId>(
        this AdapterTreeNodeModel<TId, TAltId> nodeModel, bool value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return value
            ? nodeModel.WithStatus(nodeModel.Status | AdapterNodeStatus.Synced)
            : nodeModel.WithStatus(nodeModel.Status & ~AdapterNodeStatus.Synced);
    }

    public static AdapterTreeNodeModel<TId, TAltId> WithStateUpdateFlags<TId, TAltId>(
        this AdapterTreeNodeModel<TId, TAltId> nodeModel, AdapterNodeStatus value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return nodeModel.WithStatus(
            (nodeModel.Status & ~AdapterNodeStatus.StateUpdateFlagsMask) |
            (value & AdapterNodeStatus.StateUpdateFlagsMask));
    }

    public static bool IsStateUpdatePending<TId, TAltId>(
        this AdapterTreeNodeModel<TId, TAltId> nodeModel)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return nodeModel.Status.HasAnyFlag(AdapterNodeStatus.StateUpdateFlagsMask);
    }

    public static bool IsHydrationPending<TId, TAltId>(
        this AdapterTreeNodeModel<TId, TAltId> nodeModel)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return nodeModel.Status.HasFlag(AdapterNodeStatus.HydrationPending);
    }

    public static AdapterTreeNodeModel<TId, TAltId> WithStatus<TId, TAltId>(this AdapterTreeNodeModel<TId, TAltId> nodeModel, AdapterNodeStatus value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        nodeModel.Status = value;

        return nodeModel;
    }

    public static bool IsDirtyPlaceholder<TId, TAltId>(this AdapterTreeNodeModel<TId, TAltId> nodeModel)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return nodeModel.Status.HasFlag(AdapterNodeStatus.DirtyPlaceholder);
    }

    public static bool ContentHasChangedRecently<TId, TAltId>(this AdapterTreeNodeModel<TId, TAltId> nodeModel, TimeSpan minDurationSinceLastWrite)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        var durationSinceLastWriteTime = (DateTime.UtcNow - nodeModel.LastWriteTime).Duration();
        return durationSinceLastWriteTime <= minDurationSinceLastWrite;
    }

    public static bool HasDirtyAttributes<TId, TAltId>(this AdapterTreeNodeModel<TId, TAltId> nodeModel)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return nodeModel.Status.HasFlag(AdapterNodeStatus.DirtyAttributes);
    }

    public static bool HasDirtyChildrenFlag<TId, TAltId>(this AdapterTreeNodeModel<TId, TAltId> nodeModel)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return nodeModel.Status.HasFlag(AdapterNodeStatus.DirtyChildren);
    }

    public static bool HasDirtyDescendantsFlag<TId, TAltId>(this AdapterTreeNodeModel<TId, TAltId> nodeModel)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return nodeModel.Status.HasFlag(AdapterNodeStatus.DirtyDescendants);
    }

    public static bool IsLostOrDeleted<TId, TAltId>(this AdapterTreeNodeModel<TId, TAltId> nodeModel)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return nodeModel.Status.HasAnyFlag(AdapterNodeStatus.DirtyParent | AdapterNodeStatus.DirtyDeleted);
    }

    public static bool IsStable<TId, TAltId>(this AdapterTreeNodeModel<TId, TAltId> nodeModel)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return !nodeModel.IsDirtyPlaceholder() &&
               !nodeModel.IsLostOrDeleted();
    }

    public static AdapterTreeNodeModel<TId, TAltId> WithAltId<TId, TAltId>(
        this AdapterTreeNodeModel<TId, TAltId> nodeModel, LooseCompoundAltIdentity<TAltId> value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        nodeModel.AltId = value;
        return nodeModel;
    }

    public static AdapterTreeNodeModel<TId, TAltId> WithRevisionId<TId, TAltId>(
        this AdapterTreeNodeModel<TId, TAltId> nodeModel, string? value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        nodeModel.RevisionId = value;
        return nodeModel;
    }

    public static AdapterTreeNodeModel<TId, TAltId> WithLastWriteTime<TId, TAltId>(
        this AdapterTreeNodeModel<TId, TAltId> nodeModel, DateTime value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        nodeModel.LastWriteTime = value;
        return nodeModel;
    }

    public static AdapterTreeNodeModel<TId, TAltId> WithSize<TId, TAltId>(
        this AdapterTreeNodeModel<TId, TAltId> nodeModel, long value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        nodeModel.Size = value;
        return nodeModel;
    }

    public static AdapterTreeNodeModel<TId, TAltId> WithContentVersion<TId, TAltId>(
        this AdapterTreeNodeModel<TId, TAltId> nodeModel, long value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        nodeModel.ContentVersion = value;
        return nodeModel;
    }
}
