using System;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.Trees.StateMaintenance;

public static class StateMaintenanceTreeNodeModelExtensions
{
    private const string UnusedTreeNodeName = ".";

    public static StateMaintenanceTreeNodeModel<TId> WithStatus<TId>(this StateMaintenanceTreeNodeModel<TId> nodeModel, AdapterNodeStatus value)
        where TId : IEquatable<TId>
    {
        nodeModel.Status = value;

        return nodeModel;
    }

    /// <summary>
    /// The State Maintenance Tree contains only nodes that are candidate for file sync state update
    /// on the filesystem. Those are nodes with both <see cref="AdapterNodeStatus.Synced"/>
    /// and <see cref="AdapterNodeStatus.StateUpdatePending"/> flags set.
    /// </summary>
    public static bool IsCandidateForSyncStateUpdate<TId, TAltId>(
        this AdapterTreeNodeModel<TId, TAltId> nodeModel)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        return IsCandidateForSyncStateUpdate(nodeModel.Status);
    }

    /// <summary>
    /// The State Maintenance Tree contains only nodes that are candidate for file sync state update
    /// on the filesystem. Those are nodes with both <see cref="AdapterNodeStatus.Synced"/>
    /// and <see cref="AdapterNodeStatus.StateUpdatePending"/> flags set.
    /// </summary>
    public static bool IsCandidateForSyncStateUpdate<TId>(
        this StateMaintenanceTreeNodeModel<TId> nodeModel)
        where TId : IEquatable<TId>
    {
        return IsCandidateForSyncStateUpdate(nodeModel.Status);
    }

    public static StateMaintenanceTreeNodeModel<TId> ToStateMaintenanceTreeNodeModel<TId, TAltId>(
        this AdapterTreeNodeModel<TId, TAltId> nodeModel)
        where TId : IEquatable<TId>
        where TAltId : IEquatable<TAltId>
    {
        var isCandidateForSyncStateUpdate = IsCandidateForSyncStateUpdate(nodeModel.Status);

        // Only Synced, StateUpdatePending, and HydrationPending flags are retained for nodes requiring
        // file sync state update on the filesystem.
        var status = isCandidateForSyncStateUpdate
            ? nodeModel.Status & (AdapterNodeStatus.Synced | AdapterNodeStatus.StateUpdateFlagsMask)
            : AdapterNodeStatus.None;

        // The node names are not used in the State Maintenance Tree, but they must be not empty for operations to execute
        return new StateMaintenanceTreeNodeModel<TId>()
            .WithId(nodeModel.Id)
            .WithParentId(nodeModel.ParentId)
            .WithName<StateMaintenanceTreeNodeModel<TId>, TId>(UnusedTreeNodeName)
            .WithStatus(status);
    }

    public static bool IsHydrationPending<TId>(
        this StateMaintenanceTreeNodeModel<TId> nodeModel)
        where TId : IEquatable<TId>
    {
        return nodeModel.Status.HasFlag(AdapterNodeStatus.HydrationPending);
    }

    private static bool IsCandidateForSyncStateUpdate(AdapterNodeStatus status)
    {
        return status.HasFlag(AdapterNodeStatus.Synced) && status.HasAnyFlag(AdapterNodeStatus.StateUpdateFlagsMask);
    }
}
