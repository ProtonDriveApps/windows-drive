using Proton.Drive.Sdk.Sync.Engine.Shared;
using Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Propagation;
using Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Synced;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Trees;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Engine.ConflictResolution;

internal class DeleteConflictResolutionPipeline<TId>
    where TId : IComparable<TId>, IEquatable<TId>
{
    private readonly IFileNameFactory<TId> _nameFactory;

    private readonly SyncRootLocator<TId> _syncRoot;

    public DeleteConflictResolutionPipeline(
        SyncedTree<TId> syncedTree,
        PropagationTree<TId> propagationTree,
        IFileNameFactory<TId> nameFactory)
    {
        _nameFactory = nameFactory;

        _syncRoot = new SyncRootLocator<TId>(syncedTree, propagationTree);
    }

    public PropagationTreeNodeModel<TId> Execute(
        PropagationTreeNodeModel<TId> nodeModel,
        ConflictType conflictType)
    {
        switch (conflictType)
        {
            case ConflictType.None:
                return nodeModel;

            case ConflictType.EditDelete:
            case ConflictType.MoveDelete:
                return ResolveDeletion(nodeModel);

            case ConflictType.EditParentDelete:
                return ResolveParentDeletion(nodeModel);

            default:
                throw new InvalidOperationException($"Invalid {nameof(conflictType)} value {conflictType}");
        }
    }

    private static bool IsDeleted(UpdateStatus status)
    {
        return status.Contains(UpdateStatus.Deleted);
    }

    private PropagationTreeNodeModel<TId> ResolveDeletion(PropagationTreeNodeModel<TId> nodeModel)
    {
        // Edit/Move always wins - restore the deleted node
        return Restore(nodeModel);
    }

    private PropagationTreeNodeModel<TId> ResolveParentDeletion(PropagationTreeNodeModel<TId> nodeModel)
    {
        // Edit/Move always wins - restore the deleted node on the sync root
        return MoveToSyncRoot(Restore(nodeModel));
    }

    private PropagationTreeNodeModel<TId> MoveToSyncRoot(PropagationTreeNodeModel<TId> nodeModel)
    {
        var model = nodeModel.Copy()
            .WithParentId(GetSyncRootNodeId(nodeModel))
            .WithName<PropagationTreeNodeModel<TId>, TId>(_nameFactory.GetName(nodeModel));

        return model;
    }

    private PropagationTreeNodeModel<TId> Restore(PropagationTreeNodeModel<TId> nodeModel)
    {
        var remoteStatus = nodeModel.LocalStatus;
        var localStatus = nodeModel.RemoteStatus;

        var model = nodeModel
            .WithRemoteStatus(IsDeleted(remoteStatus) ? UpdateStatus.Created | UpdateStatus.Restore : UpdateStatus.Unchanged)
            .WithLocalStatus(IsDeleted(localStatus) ? UpdateStatus.Created | UpdateStatus.Restore : UpdateStatus.Unchanged);

        return model;
    }

    private TId GetSyncRootNodeId(IIdentifiableTreeNode<TId> nodeModel)
    {
        return _syncRoot.GetSyncRootNodeId(nodeModel);
    }
}
