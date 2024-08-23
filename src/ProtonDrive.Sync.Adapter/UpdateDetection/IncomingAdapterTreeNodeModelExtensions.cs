using System;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.Sync.Adapter.UpdateDetection;

public static class IncomingAdapterTreeNodeModelExtensions
{
    public static bool IsStateUpdatePending<TId, TAltId>(
        this IncomingAdapterTreeNodeModel<TId, TAltId> nodeModel)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return nodeModel.Status.HasAnyFlag(AdapterNodeStatus.StateUpdateFlagsMask);
    }

    public static IncomingAdapterTreeNodeModel<TId, TAltId> WithStateUpdateFlags<TId, TAltId>(
        this IncomingAdapterTreeNodeModel<TId, TAltId> nodeModel, AdapterNodeStatus value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return nodeModel.WithStatus(
            (nodeModel.Status & ~AdapterNodeStatus.StateUpdateFlagsMask) |
            (value & AdapterNodeStatus.StateUpdateFlagsMask));
    }

    public static IncomingAdapterTreeNodeModel<TId, TAltId> WithDirtyFlags<TId, TAltId>(
        this IncomingAdapterTreeNodeModel<TId, TAltId> nodeModel, AdapterNodeStatus value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return nodeModel
            .WithStatus(nodeModel.Status & ~AdapterNodeStatus.DirtyMask | value);
    }

    public static IncomingAdapterTreeNodeModel<TId, TAltId> WithAppendedDirtyFlags<TId, TAltId>(
        this IncomingAdapterTreeNodeModel<TId, TAltId> nodeModel, AdapterNodeStatus value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return nodeModel
        .WithStatus(nodeModel.Status | value);
    }

    public static IncomingAdapterTreeNodeModel<TId, TAltId> WithRemovedDirtyFlags<TId, TAltId>(
        this IncomingAdapterTreeNodeModel<TId, TAltId> nodeModel, AdapterNodeStatus value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return nodeModel
            .WithStatus(nodeModel.Status & ~value);
    }

    public static IncomingAdapterTreeNodeModel<TId, TAltId> WithAltId<TId, TAltId>(
        this IncomingAdapterTreeNodeModel<TId, TAltId> nodeModel, LooseCompoundAltIdentity<TAltId> value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        nodeModel.AltId = value;

        return nodeModel;
    }

    public static IncomingAdapterTreeNodeModel<TId, TAltId> WithStatus<TId, TAltId>(
        this IncomingAdapterTreeNodeModel<TId, TAltId> nodeModel, AdapterNodeStatus value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        nodeModel.Status = value;
        return nodeModel;
    }

    public static IncomingAdapterTreeNodeModel<TId, TAltId> WithContentVersion<TId, TAltId>(
        this IncomingAdapterTreeNodeModel<TId, TAltId> nodeModel, long value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        nodeModel.ContentVersion = value;

        return nodeModel;
    }

    public static IncomingAdapterTreeNodeModel<TId, TAltId> WithRevisionId<TId, TAltId>(
        this IncomingAdapterTreeNodeModel<TId, TAltId> nodeModel, string? value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        nodeModel.RevisionId = value;

        return nodeModel;
    }

    public static IncomingAdapterTreeNodeModel<TId, TAltId> WithLastWriteTime<TId, TAltId>(
        this IncomingAdapterTreeNodeModel<TId, TAltId> nodeModel, DateTime value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        nodeModel.LastWriteTime = value;

        return nodeModel;
    }

    public static IncomingAdapterTreeNodeModel<TId, TAltId> WithSize<TId, TAltId>(
        this IncomingAdapterTreeNodeModel<TId, TAltId> nodeModel, long value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        nodeModel.Size = value;

        return nodeModel;
    }

    public static IncomingAdapterTreeNodeModel<TId, TAltId> WithSizeOnStorage<TId, TAltId>(
        this IncomingAdapterTreeNodeModel<TId, TAltId> nodeModel, long? value)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        nodeModel.SizeOnStorage = value;

        return nodeModel;
    }
}
