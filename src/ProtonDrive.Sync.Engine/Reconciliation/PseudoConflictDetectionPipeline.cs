using System;
using System.Collections.Generic;
using System.Linq;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.Reconciliation;

internal class PseudoConflictDetectionPipeline<TId>
    where TId : IEquatable<TId>
{
    private readonly SyncedTree<TId> _syncedTree;
    private readonly UpdateTree<TId> _remoteUpdateTree;
    private readonly UpdateTree<TId> _localUpdateTree;
    private readonly PropagationTree<TId> _propagationTree;

    private readonly FileContentEqualityComparer<TId> _contentEqualityComparer = new();
    private readonly PreparationPipeline<TId> _preparationPipeline;

    public PseudoConflictDetectionPipeline(
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> remoteUpdateTree,
        UpdateTree<TId> localUpdateTree,
        PropagationTree<TId> propagationTree)
    {
        _syncedTree = syncedTree;
        _remoteUpdateTree = remoteUpdateTree;
        _localUpdateTree = localUpdateTree;
        _propagationTree = propagationTree;

        _preparationPipeline = new PreparationPipeline<TId>(syncedTree, remoteUpdateTree, localUpdateTree);
    }

    public (ConflictType ConflictType, UpdateTreeNodeModel<TId>? ConflictingNodeModel) CreateCreatePseudoConflict(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel)
    {
        // Create-Create (Pseudo)
        if (remoteNodeModel.Status.Contains(UpdateStatus.Created))
        {
            var conflictingNodeModel = ConflictingLocalNode(remoteNodeModel)?.Model;

            if (conflictingNodeModel != null)
            {
                // The conflictingNode has the same parent in the PropagationTree and the same name as remoteNodeModel.
                if (conflictingNodeModel.Type == NodeType.Directory ||
                    conflictingNodeModel.Type == NodeType.File &&
                    _contentEqualityComparer.Equals(remoteNodeModel, conflictingNodeModel)
                   )
                {
                    return (ConflictType.CreateCreatePseudo, conflictingNodeModel.Copy());
                }
            }
        }

        // Create-Create (Pseudo)
        if (localNodeModel.Status.Contains(UpdateStatus.Created))
        {
            var conflictingNodeModel = ConflictingRemoteNode(localNodeModel)?.Model;

            if (conflictingNodeModel != null)
            {
                // The conflictingNode has the same parent in the PropagationTree and the same name as localNodeModel.
                if (conflictingNodeModel.Type == NodeType.Directory ||
                    conflictingNodeModel.Type == NodeType.File &&
                    _contentEqualityComparer.Equals(localNodeModel, conflictingNodeModel)
                   )
                {
                    return (ConflictType.CreateCreatePseudo, MappedFromRemoteNodeModel(conflictingNodeModel));
                }
            }
        }

        return (ConflictType.None, null);
    }

    public IEnumerable<(ConflictType ConflictType, UpdateStatus ConflictingStatus)> PseudoConflict(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel)
    {
        // At the same time nodes can participate in at most two pseudo conflicts:
        // Edit-Edit and Move-Move.

        var conflictingStatus = remoteNodeModel.Status.Intersect(localNodeModel.Status);

        // Edit-Edit (Pseudo)
        if (conflictingStatus.Contains(UpdateStatus.Edited) &&
            _contentEqualityComparer.Equals(remoteNodeModel, localNodeModel))
        {
            yield return (ConflictType.EditEditPseudo, UpdateStatus.Edited);
        }

        // Move-Move (Pseudo)
        var status = UpdateStatus.Unchanged;
        if (conflictingStatus.Contains(UpdateStatus.Renamed) && remoteNodeModel.Name == localNodeModel.Name)
        {
            status = status.Union(UpdateStatus.Renamed);
        }

        if (conflictingStatus.Contains(UpdateStatus.Moved) && remoteNodeModel.ParentId.Equals(localNodeModel.ParentId))
        {
            status = status.Union(UpdateStatus.Moved);
        }

        if (status != UpdateStatus.Unchanged)
        {
            yield return (ConflictType.MoveMovePseudo, status);
        }

        // Delete-Delete (Pseudo)
        if (conflictingStatus.Contains(UpdateStatus.Deleted))
        {
            yield return (ConflictType.DeleteDeletePseudo, UpdateStatus.Deleted);
        }
    }

    private UpdateTreeNodeModel<TId> MappedFromRemoteNodeModel(UpdateTreeNodeModel<TId> remoteNodeModel)
    {
        return _preparationPipeline.MappedFromRemoteNodeModel(remoteNodeModel);
    }

    private UpdateTreeNode<TId>? ConflictingLocalNode(UpdateTreeNodeModel<TId> remoteNodeModel)
    {
        var parentNode = LocalParentNode(remoteNodeModel);

        return ConflictingNode(parentNode, remoteNodeModel);
    }

    private UpdateTreeNode<TId>? ConflictingRemoteNode(UpdateTreeNodeModel<TId> localNodeModel)
    {
        var parentNode = RemoteParentNode(localNodeModel);

        return ConflictingNode(parentNode, localNodeModel);
    }

    private UpdateTreeNode<TId>? ConflictingNode(UpdateTreeNode<TId>? parentNode, UpdateTreeNodeModel<TId> nodeModel)
    {
        var node = parentNode?
            .ChildrenByName(nodeModel.Name)
            .FirstOrDefault(c =>
                c.Type == nodeModel.Type &&
                c.Model.Status == UpdateStatus.Created &&
                c.Name == nodeModel.Name);

        return node;
    }

    private UpdateTreeNode<TId>? LocalParentNode(UpdateTreeNodeModel<TId> remoteNodeModel)
    {
        // Id and ParentId values of remoteNodeModel are mapped to local ID values.

        var propagationParent = _propagationTree.NodeByIdOrDefault(remoteNodeModel.ParentId);
        if (propagationParent != null)
        {
            return _localUpdateTree.NodeByIdOrDefault(propagationParent.Model.Id);
        }

        var syncedParent = _syncedTree.NodeByIdOrDefault(remoteNodeModel.ParentId);
        if (syncedParent != null)
        {
            return _localUpdateTree.NodeByIdOrDefault(syncedParent.Model.Id);
        }

        return null;
    }

    private UpdateTreeNode<TId>? RemoteParentNode(UpdateTreeNodeModel<TId> localNodeModel)
    {
        var propagationParent = _propagationTree.NodeByIdOrDefault(localNodeModel.ParentId);
        if (propagationParent != null)
        {
            return _remoteUpdateTree.NodeByIdOrDefault(propagationParent.AltId);
        }

        var syncedParent = _syncedTree.NodeByIdOrDefault(localNodeModel.ParentId);
        if (syncedParent != null)
        {
            return _remoteUpdateTree.NodeByIdOrDefault(syncedParent.AltId);
        }

        return null;
    }
}
