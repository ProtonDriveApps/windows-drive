using System;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.ConflictResolution;

internal class IndirectConflictResolutionPipeline<TId>
    where TId : IComparable<TId>, IEquatable<TId>
{
    private readonly SyncedTree<TId> _syncedTree;
    private readonly IFileNameFactory<TId> _nameFactory;

    private readonly CyclicMove<TId> _cyclicMove;
    private readonly NameClash<TId> _nameClash;
    private readonly NearestPropagationTreeAncestor<TId> _nearestAncestor;
    private readonly SyncRootLocator<TId> _syncRoot;

    public IndirectConflictResolutionPipeline(
        SyncedTree<TId> syncedTree,
        PropagationTree<TId> propagationTree,
        IFileNameFactory<TId> nameFactory)
    {
        _syncedTree = syncedTree;
        _nameFactory = nameFactory;

        _cyclicMove = new CyclicMove<TId>(syncedTree, propagationTree);
        _nameClash = new NameClash<TId>(syncedTree, propagationTree);
        _nearestAncestor = new NearestPropagationTreeAncestor<TId>(syncedTree, propagationTree);
        _syncRoot = new SyncRootLocator<TId>(syncedTree, propagationTree);
    }

    public UpdateTreeNodeModel<TId> Execute(UpdateTreeNodeModel<TId> nodeModel, ConflictType conflictType)
    {
        switch (conflictType)
        {
            case ConflictType.None:
                return nodeModel;

            case ConflictType.MoveParentDeleteDest:
            case ConflictType.MoveMoveCycle:
                return UndoMove(nodeModel);

            case ConflictType.CreateParentDelete:
                return MoveToSyncRoot(nodeModel);

            default:
                throw new InvalidOperationException($"Invalid {nameof(conflictType)} value {conflictType}");
        }
    }

    private UpdateTreeNodeModel<TId> UndoMove(UpdateTreeNodeModel<TId> nodeModel)
    {
        nodeModel = MoveToOrigin(nodeModel);

        if (IsParentDeleted(nodeModel) ||
            IsCyclicMove(nodeModel) ||
            IsNameClash(nodeModel))
        {
            return MoveToSyncRoot(nodeModel);
        }

        return nodeModel;
    }

    private UpdateTreeNodeModel<TId> MoveToSyncRoot(UpdateTreeNodeModel<TId> nodeModel)
    {
        var model = nodeModel
            .WithParentId(GetSyncRootNodeId(nodeModel))
            .WithName<UpdateTreeNodeModel<TId>, TId>(_nameFactory.GetName(nodeModel));

        return model;
    }

    private UpdateTreeNodeModel<TId> MoveToOrigin(UpdateTreeNodeModel<TId> nodeModel)
    {
        var syncedModel = _syncedTree.NodeByIdOrDefault(nodeModel.Id)?.Model;
        if (syncedModel == null)
        {
            throw new InvalidOperationException($"SyncedTree node with Id={nodeModel.Id} does not exist");
        }

        var model = nodeModel
            .WithLinkFrom(syncedModel);

        return model;
    }

    private bool IsParentDeleted(IIdentifiableTreeNode<TId> nodeModel)
    {
        return _nearestAncestor.IsDeleted(nodeModel);
    }

    private bool IsCyclicMove(IIdentifiableTreeNode<TId> nodeModel)
    {
        return _cyclicMove.Exists(nodeModel);
    }

    private bool IsNameClash(IFileSystemNodeModel<TId> nodeModel)
    {
        return _nameClash.Exists(nodeModel);
    }

    private TId GetSyncRootNodeId(IIdentifiableTreeNode<TId> nodeModel)
    {
        return _syncRoot.GetSyncRootNodeId(nodeModel);
    }
}
