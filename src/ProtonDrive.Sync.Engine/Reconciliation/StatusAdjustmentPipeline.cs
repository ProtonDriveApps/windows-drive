using System;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.Reconciliation;

internal class StatusAdjustmentPipeline<TId>
    where TId : IEquatable<TId>
{
    private readonly SyncedTree<TId> _syncedTree;
    private readonly UpdateTree<TId> _remoteUpdateTree;
    private readonly UpdateTree<TId> _localUpdateTree;

    private readonly NearestPropagationTreeAncestor<TId> _nearestAncestor;

    public StatusAdjustmentPipeline(
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> remoteUpdateTree,
        UpdateTree<TId> localUpdateTree,
        PropagationTree<TId> propagationTree)
    {
        _syncedTree = syncedTree;
        _remoteUpdateTree = remoteUpdateTree;
        _localUpdateTree = localUpdateTree;

        _nearestAncestor = new NearestPropagationTreeAncestor<TId>(syncedTree, propagationTree);
    }

    public PropagationTreeNodeModel<TId> Execute(PropagationTreeNodeModel<TId> nodeModel)
    {
        Ensure.NotNull(nodeModel, nameof(nodeModel));

        return WithAdjustedStatus(nodeModel);
    }

    private PropagationTreeNodeModel<TId> WithAdjustedStatus(PropagationTreeNodeModel<TId> nodeModel)
    {
        if (nodeModel.RemoteStatus.Contains(UpdateStatus.Deleted) ||
            nodeModel.LocalStatus.Contains(UpdateStatus.Deleted))
        {
            // Skip adding deleted node when parent is already deleted
            if (ParentDeleted(nodeModel))
            {
                nodeModel = nodeModel
                    .WithLocalStatus(UpdateStatus.Unchanged)
                    .WithRemoteStatus(UpdateStatus.Unchanged);
            }

            return nodeModel;
        }

        var previousCommonStatus = nodeModel.RemoteStatus.Intersect(nodeModel.LocalStatus);

        var model = WithAdjustedLocalStatus(WithAdjustedRemoteStatus(nodeModel));

        var missingStatus = previousCommonStatus
            .Minus(model.RemoteStatus)
            .Minus(model.LocalStatus)
            .Intersect(UpdateStatus.RenamedAndMoved);

        return model
            .WithRemoteStatus(model.RemoteStatus.Union(missingStatus))
            .WithLocalStatus(model.LocalStatus.Union(missingStatus));
    }

    private PropagationTreeNodeModel<TId> WithAdjustedRemoteStatus(PropagationTreeNodeModel<TId> nodeModel)
    {
        if (nodeModel.RemoteStatus.Contains(UpdateStatus.Created))
        {
            return nodeModel;
        }

        var remoteNodeModel = _remoteUpdateTree.NodeByIdOrDefault(nodeModel.AltId)?.Model.Copy();
        if (remoteNodeModel != null)
        {
            var parentNode = _syncedTree.NodeByAltIdOrDefault(remoteNodeModel.ParentId);
            var parentId = parentNode != null ? parentNode.Id : remoteNodeModel.ParentId;
            remoteNodeModel = remoteNodeModel.WithParentId(parentId);
        }

        var originalModel = remoteNodeModel ??
                            (IFileSystemNodeModel<TId>?)_syncedTree.NodeByIdOrDefault(nodeModel.Id)?.Model;

        return nodeModel.WithRemoteStatus(nodeModel.RemoteStatus
            .Minus(UpdateStatus.RenamedAndMoved)
            .Union(RenamedAndOrMovedStatus(nodeModel, originalModel)));
    }

    private PropagationTreeNodeModel<TId> WithAdjustedLocalStatus(PropagationTreeNodeModel<TId> nodeModel)
    {
        if (nodeModel.LocalStatus.Contains(UpdateStatus.Created))
        {
            return nodeModel;
        }

        var originalModel = _localUpdateTree.NodeByIdOrDefault(nodeModel.Id)?.Model.Copy() ??
                            (IFileSystemNodeModel<TId>?)_syncedTree.NodeByIdOrDefault(nodeModel.Id)?.Model;

        return nodeModel.WithLocalStatus(nodeModel.LocalStatus
            .Minus(UpdateStatus.RenamedAndMoved)
            .Union(RenamedAndOrMovedStatus(nodeModel, originalModel)));
    }

    private UpdateStatus RenamedAndOrMovedStatus(PropagationTreeNodeModel<TId> nodeModel, IFileSystemNodeModel<TId>? originalNodeModel)
    {
        var status = UpdateStatus.Unchanged;

        if (originalNodeModel == null)
        {
            return status;
        }

        if (nodeModel.Name != originalNodeModel.Name)
        {
            status = status.Union(UpdateStatus.Renamed);
        }

        if (!nodeModel.ParentId.Equals(originalNodeModel.ParentId))
        {
            status = status.Union(UpdateStatus.Moved);
        }

        return status;
    }

    private bool ParentDeleted(IIdentifiableTreeNode<TId> nodeModel)
    {
        return _nearestAncestor.IsDeleted(nodeModel);
    }
}
