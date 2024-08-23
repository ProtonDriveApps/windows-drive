using System;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Engine.Reconciliation;

internal class PreparationPipeline<TId>
    where TId : IEquatable<TId>
{
    private readonly SyncedTree<TId> _syncedTree;
    private readonly UpdateTree<TId> _remoteUpdateTree;
    private readonly UpdateTree<TId> _localUpdateTree;

    public PreparationPipeline(
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> remoteUpdateTree,
        UpdateTree<TId> localUpdateTree)
    {
        _syncedTree = syncedTree;
        _remoteUpdateTree = remoteUpdateTree;
        _localUpdateTree = localUpdateTree;
    }

    public (UpdateTreeNodeModel<TId> Remote, UpdateTreeNodeModel<TId> Local) Execute(
        UpdateTreeNode<TId>? remoteNode,
        UpdateTreeNode<TId>? localNode)
    {
        Ensure.IsFalse(remoteNode == null && localNode == null, $"At least one of {nameof(remoteNode)} and {nameof(localNode)} must not be null");

        var remoteNodeModel = RemoteNodeModel(remoteNode, localNode);
        var localNodeModel = LocalNodeModel(remoteNode, localNode);

        return (remoteNodeModel, localNodeModel);
    }

    public UpdateTreeNodeModel<TId> MappedFromRemoteNodeModel(UpdateTreeNodeModel<TId> remoteNodeModel)
    {
        var syncedNode = _syncedTree.NodeByAltIdOrDefault(remoteNodeModel.Id);
        var parentNode = _syncedTree.NodeByAltIdOrDefault(remoteNodeModel.ParentId);

        var id = syncedNode != null ? syncedNode.Id : remoteNodeModel.Id;
        var parentId = parentNode != null ? parentNode.Id : remoteNodeModel.ParentId;

        return remoteNodeModel.Copy()
            .WithId(id)
            .WithParentId(parentId);
    }

    private UpdateTreeNodeModel<TId> LocalNodeModel(
        UpdateTreeNode<TId>? remoteNode,
        UpdateTreeNode<TId>? localNode)
    {
        if (localNode != null)
        {
            return localNode.Model.Copy();
        }

        if ((remoteNode!.Model.Status & UpdateStatus.All) == UpdateStatus.Created)
        {
            // When other node is Created, there is no corresponding node in SyncedTree and
            // own UpdateTree.
            return MappedFromRemoteNodeModel(remoteNode.Model).WithStatus(UpdateStatus.Unchanged);
        }

        // When other node is not Created, the corresponding SyncedTree node exists
        var syncedNode = _syncedTree.NodeByAltId(remoteNode.Model.Id);

        var node = _localUpdateTree.NodeByIdOrDefault(syncedNode.Model.Id);
        if (node != null)
        {
            return node.Model.Copy();
        }

        var nodeModel = new UpdateTreeNodeModel<TId>()
            .CopiedFrom(syncedNode.Model);

        // When the node is deleted indirectly, due to its parent is deleted, then returned node model
        // contains Deleted status. Except when both remote and local nodes are Unchanged.
        if (remoteNode.Model.Status != UpdateStatus.Unchanged && LocalParentDeleted(syncedNode))
        {
            return nodeModel.WithStatus(UpdateStatus.Deleted);
        }

        return nodeModel;
    }

    private UpdateTreeNodeModel<TId> RemoteNodeModel(
        UpdateTreeNode<TId>? remoteNode,
        UpdateTreeNode<TId>? localNode)
    {
        if (remoteNode != null)
        {
            return MappedFromRemoteNodeModel(remoteNode.Model);
        }

        if ((localNode!.Model.Status & UpdateStatus.All) == UpdateStatus.Created)
        {
            // When other node is Created, there is no corresponding node in Synced Tree and
            // own UpdateTree.
            return localNode.Model.Copy().WithStatus(UpdateStatus.Unchanged);
        }

        // When other node is not Created, the corresponding Synced Tree node exists
        var syncedNode = _syncedTree.NodeById(localNode.Model.Id);

        var node = _remoteUpdateTree.NodeByIdOrDefault(syncedNode.AltId);
        if (node != null)
        {
            return MappedFromRemoteNodeModel(node.Model);
        }

        var nodeModel = new UpdateTreeNodeModel<TId>()
            .CopiedFrom(syncedNode.Model);

        // When the node is deleted indirectly, due to its parent is deleted, then returned node model
        // contains Deleted status. Except when both remote and local nodes are Unchanged.
        if (localNode.Model.Status != UpdateStatus.Unchanged && RemoteParentDeleted(syncedNode))
        {
            return nodeModel.WithStatus(UpdateStatus.Deleted);
        }

        return nodeModel;
    }

    private bool RemoteParentDeleted(SyncedTreeNode<TId> syncedNode)
    {
        var parentModel = NearestRemoteParent(syncedNode).Model;

        return parentModel.Status.Contains(UpdateStatus.Deleted);
    }

    private bool LocalParentDeleted(SyncedTreeNode<TId> syncedNode)
    {
        var parentModel = NearestLocalParent(syncedNode).Model;

        return parentModel.Status.Contains(UpdateStatus.Deleted);
    }

    private UpdateTreeNode<TId> NearestRemoteParent(SyncedTreeNode<TId> syncedNode)
    {
        while (!syncedNode.IsRoot)
        {
            syncedNode = syncedNode.Parent!;
            var parentNode = _remoteUpdateTree.NodeByIdOrDefault(syncedNode.AltId);
            if (parentNode != null)
            {
                return parentNode;
            }
        }

        return _remoteUpdateTree.Root;
    }

    private UpdateTreeNode<TId> NearestLocalParent(SyncedTreeNode<TId> syncedNode)
    {
        while (!syncedNode.IsRoot)
        {
            syncedNode = syncedNode.Parent!;
            var parentNode = _localUpdateTree.NodeByIdOrDefault(syncedNode.Id);
            if (parentNode != null)
            {
                return parentNode;
            }
        }

        return _localUpdateTree.Root;
    }
}
