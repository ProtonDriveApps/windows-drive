using System;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.ConflictResolution;

internal class MoveConflictResolutionPipeline<TId>
    where TId : IComparable<TId>, IEquatable<TId>
{
    public (UpdateTreeNodeModel<TId> Remote, UpdateTreeNodeModel<TId> Local) Execute(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel,
        ConflictType conflictType)
    {
        switch (conflictType)
        {
            case ConflictType.None:
                return (remoteNodeModel, localNodeModel);

            case ConflictType.MoveMoveSource:
                return Resolve(remoteNodeModel, localNodeModel);

            default:
                throw new InvalidOperationException($"Invalid {nameof(conflictType)} value {conflictType}");
        }
    }

    private (UpdateTreeNodeModel<TId> Remote, UpdateTreeNodeModel<TId> Local) Resolve(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel)
    {
        // Remote replica always wins
        localNodeModel = DiscardMove(localNodeModel, remoteNodeModel);

        return (remoteNodeModel, localNodeModel);
    }

    private UpdateTreeNodeModel<TId> DiscardMove(UpdateTreeNodeModel<TId> nodeModel, UpdateTreeNodeModel<TId> winningNodeModel)
    {
        var model = nodeModel
            .WithLinkFrom(winningNodeModel);

        return model;
    }
}
