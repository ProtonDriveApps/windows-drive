using System;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.ConflictResolution;

internal class EditConflictResolutionPipeline<TId>
    where TId : IComparable<TId>, IEquatable<TId>
{
    public (UpdateTreeNodeModel<TId> Remote, UpdateTreeNodeModel<TId> Local, bool Backup) Execute(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel,
        ConflictType conflictType)
    {
        switch (conflictType)
        {
            case ConflictType.None:
                return (remoteNodeModel, localNodeModel, false);

            case ConflictType.EditEdit:
                return Resolve(remoteNodeModel, localNodeModel);

            default:
                throw new InvalidOperationException($"Invalid {nameof(conflictType)} value {conflictType}");
        }
    }

    private (UpdateTreeNodeModel<TId> Remote, UpdateTreeNodeModel<TId> Local, bool Backup) Resolve(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel)
    {
        // Remote replica always wins
        localNodeModel = DiscardEdit(localNodeModel, remoteNodeModel);

        // Backup overwritten local file
        return (remoteNodeModel, localNodeModel, Backup: true);
    }

    private UpdateTreeNodeModel<TId> DiscardEdit(UpdateTreeNodeModel<TId> nodeModel, UpdateTreeNodeModel<TId> winningNodeModel)
    {
        var model = nodeModel
            .WithAttributesFrom(winningNodeModel)
            .WithStatus(nodeModel.Status.Minus(UpdateStatus.Edited));

        return model;
    }
}
