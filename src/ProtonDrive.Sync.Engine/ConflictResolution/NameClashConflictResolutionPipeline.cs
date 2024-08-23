using System;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.ConflictResolution;

internal class NameClashConflictResolutionPipeline<TId>
    where TId : IComparable<TId>, IEquatable<TId>
{
    private readonly PropagationTree<TId> _propagationTree;
    private readonly IFileNameFactory<TId> _nameFactory;

    public NameClashConflictResolutionPipeline(
        PropagationTree<TId> propagationTree,
        IFileNameFactory<TId> nameFactory)
    {
        _propagationTree = propagationTree;
        _nameFactory = nameFactory;
    }

    public PropagationTreeNodeModel<TId> Execute(
        PropagationTreeNodeModel<TId> nodeModel,
        PropagationTreeNodeModel<TId> otherNodeModel,
        ConflictType conflictType)
    {
        switch (conflictType)
        {
            case ConflictType.None:
                return nodeModel;

            case ConflictType.CreateCreate:
            case ConflictType.MoveCreate:
            case ConflictType.MoveMoveDest:
                return Rename(nodeModel, otherNodeModel);

            default:
                throw new InvalidOperationException($"Invalid {nameof(conflictType)} value {conflictType}");
        }
    }

    private PropagationTreeNodeModel<TId> Rename(PropagationTreeNodeModel<TId> nodeModel, PropagationTreeNodeModel<TId> otherNodeModel)
    {
        if (!IsWinner(nodeModel, otherNodeModel))
        {
            // The status is not adjusted here as it will be adjusted in the next step.
            return Rename(nodeModel);
        }

        Save(WithAdjustedStatus(Rename(otherNodeModel)));

        return nodeModel;
    }

    private bool IsWinner(PropagationTreeNodeModel<TId> nodeModel, PropagationTreeNodeModel<TId> otherNodeModel)
    {
        /* The change on remote replica always wins.
           LocalStatus reflects changes to remote replica.
           RemoteStatus reflects changes to local replica. */

        // The existing without changes wins, the incoming loses
        if (otherNodeModel.LocalStatus == UpdateStatus.Unchanged &&
            otherNodeModel.RemoteStatus == UpdateStatus.Unchanged)
        {
            return false;
        }

        // The incoming without changes wins
        if (nodeModel.LocalStatus == UpdateStatus.Unchanged &&
            nodeModel.RemoteStatus == UpdateStatus.Unchanged)
        {
            return true;
        }

        // The incoming created on remote wins
        if (nodeModel.LocalStatus.Contains(UpdateStatus.Created))
        {
            return true;
        }

        // The incoming created on local loses
        if (nodeModel.RemoteStatus.Contains(UpdateStatus.Created))
        {
            return false;
        }

        // The existing created, renamed or moved on remote wins, the incoming loses
        if (otherNodeModel.LocalStatus.Contains(UpdateStatus.Created) ||
            otherNodeModel.LocalStatus.Contains(UpdateStatus.Renamed) ||
            otherNodeModel.LocalStatus.Contains(UpdateStatus.Moved))
        {
            return false;
        }

        // The incoming renamed or moved on remote wins
        if (nodeModel.LocalStatus.Contains(UpdateStatus.Renamed) ||
            nodeModel.LocalStatus.Contains(UpdateStatus.Moved))
        {
            return true;
        }

        // The existing wins, the incoming loses
        return false;
    }

    private PropagationTreeNodeModel<TId> Rename(PropagationTreeNodeModel<TId> nodeModel)
    {
        var model = nodeModel
            .WithName<PropagationTreeNodeModel<TId>, TId>(_nameFactory.GetName(nodeModel));

        return model;
    }

    private PropagationTreeNodeModel<TId> WithAdjustedStatus(PropagationTreeNodeModel<TId> nodeModel)
    {
        return nodeModel
            .WithLocalStatus(nodeModel.LocalStatus.Union(UpdateStatus.Renamed))
            .WithRemoteStatus(nodeModel.RemoteStatus.Union(UpdateStatus.Renamed));
    }

    private void Save(PropagationTreeNodeModel<TId> nodeModel)
    {
        _propagationTree.Operations.Execute(
            new Operation<PropagationTreeNodeModel<TId>>(OperationType.Move, nodeModel));
    }
}
