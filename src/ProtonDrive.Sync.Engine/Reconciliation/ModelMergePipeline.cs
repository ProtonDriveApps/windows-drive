using System;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.Reconciliation;

internal class ModelMergePipeline<TId>
    where TId : IEquatable<TId>
{
    private readonly SyncedTree<TId> _syncedTree;

    public ModelMergePipeline(SyncedTree<TId> syncedTree)
    {
        _syncedTree = syncedTree;
    }

    public PropagationTreeNodeModel<TId> Merged(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel,
        bool backup)
    {
        return WithCorrectAltId(MergedModel(remoteNodeModel, localNodeModel)).WithBackup(backup);
    }

    private PropagationTreeNodeModel<TId> MergedModel(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel)
    {
        var model = new PropagationTreeNodeModel<TId>()
            .WithRemoteStatus(localNodeModel.Status)
            .WithLocalStatus(remoteNodeModel.Status)
            .WithId(localNodeModel.Id)
            .WithAltId(remoteNodeModel.Id);

        if (remoteNodeModel.Status == UpdateStatus.Unchanged ||
            remoteNodeModel.Status.Contains(UpdateStatus.Deleted))
        {
            return model.CopiedFrom(localNodeModel);
        }

        model = model.CopiedFrom(remoteNodeModel)
            .WithId(localNodeModel.Id);

        if (localNodeModel.Status == UpdateStatus.Unchanged ||
            localNodeModel.Status.Contains(UpdateStatus.Deleted))
        {
            return model;
        }

        if (localNodeModel.Status.Contains(UpdateStatus.Edited))
        {
            model = model.WithAttributesFrom(localNodeModel);
        }

        if (localNodeModel.Status.Contains(UpdateStatus.Renamed))
        {
            model = model.WithName<PropagationTreeNodeModel<TId>, TId>(localNodeModel.Name);
        }

        if (localNodeModel.Status.Contains(UpdateStatus.Moved))
        {
            model = model.WithParentId(localNodeModel.ParentId);
        }

        // Deleted node is restored
        if (localNodeModel.Status == UpdateStatus.Unchanged &&
            remoteNodeModel.Status.Contains(UpdateStatus.Deleted | UpdateStatus.Restore))
        {
            model = model.WithLocalStatus(UpdateStatus.Unchanged).WithRemoteStatus(UpdateStatus.Created | UpdateStatus.Restore);
        }
        else if (remoteNodeModel.Status == UpdateStatus.Unchanged &&
                 localNodeModel.Status.Contains(UpdateStatus.Deleted | UpdateStatus.Restore))
        {
            model = model.WithRemoteStatus(UpdateStatus.Unchanged).WithLocalStatus(UpdateStatus.Created | UpdateStatus.Restore);
        }

        return model;
    }

    private PropagationTreeNodeModel<TId> WithCorrectAltId(PropagationTreeNodeModel<TId> nodeModel)
    {
        var syncedNode = _syncedTree.NodeByIdOrDefault(nodeModel.Id);

        return nodeModel.WithAltId(syncedNode != null ? syncedNode.AltId : nodeModel.AltId);
    }
}
