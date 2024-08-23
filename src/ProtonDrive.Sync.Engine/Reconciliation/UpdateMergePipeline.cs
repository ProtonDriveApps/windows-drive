using System;
using System.Collections.Generic;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Engine.ConflictResolution;
using ProtonDrive.Sync.Engine.Shared;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem.Traversal;

namespace ProtonDrive.Sync.Engine.Reconciliation;

internal class UpdateMergePipeline<TId>
    where TId : IComparable<TId>, IEquatable<TId>
{
    private readonly SyncedTree<TId> _syncedTree;
    private readonly UpdateTree<TId> _remoteUpdateTree;
    private readonly UpdateTree<TId> _localUpdateTree;
    private readonly PropagationTree<TId> _propagationTree;

    private readonly PreparationPipeline<TId> _preparationPipeline;
    private readonly PseudoConflictDetectionPipeline<TId> _pseudoConflictDetection;
    private readonly ConflictDetectionPipeline<TId> _conflictDetection;
    private readonly PseudoConflictResolutionPipeline<TId> _pseudoConflictResolution;
    private readonly IndirectConflictResolutionPipeline<TId> _indirectConflictResolution;
    private readonly NameClashConflictResolutionPipeline<TId> _nameClashConflictResolution;
    private readonly MoveConflictResolutionPipeline<TId> _moveConflictResolution;
    private readonly EditConflictResolutionPipeline<TId> _editConflictResolution;
    private readonly ModelMergePipeline<TId> _modelMerge;
    private readonly DeleteConflictResolutionPipeline<TId> _deleteConflictResolution;
    private readonly StatusAdjustmentPipeline<TId> _statusAdjustment;
    private readonly ApplyToPropagationTreePipeline<TId> _applyToPropagationTree;

    private readonly ActiveTreeTraversal<SyncedTree<TId>, SyncedTreeNode<TId>, SyncedTreeNodeModel<TId>, TId>
        _syncedTreeTraversal;
    private readonly PassiveTreeTraversal<PropagationTree<TId>, PropagationTreeNode<TId>, PropagationTreeNodeModel<TId>, TId>
        _propagationTreeTraversal;

    private readonly HashSet<TId> _processedNodeIds = new();

    private bool _processingFolderDeletion;

    public UpdateMergePipeline(
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> remoteUpdateTree,
        UpdateTree<TId> localUpdateTree,
        PropagationTree<TId> propagationTree,
        IFileNameFactory<TId> nameClashConflictNameFactory,
        IFileNameFactory<TId> deleteConflictNameFactory)
    {
        _syncedTree = syncedTree;
        _remoteUpdateTree = remoteUpdateTree;
        _localUpdateTree = localUpdateTree;
        _propagationTree = propagationTree;

        _preparationPipeline =
            new PreparationPipeline<TId>(syncedTree, remoteUpdateTree, localUpdateTree);
        _pseudoConflictDetection = new PseudoConflictDetectionPipeline<TId>(syncedTree, remoteUpdateTree, localUpdateTree, propagationTree);
        _conflictDetection = new ConflictDetectionPipeline<TId>(syncedTree, remoteUpdateTree, localUpdateTree, propagationTree);
        _pseudoConflictResolution =
            new PseudoConflictResolutionPipeline<TId>(syncedTree, remoteUpdateTree, localUpdateTree);
        _indirectConflictResolution =
            new IndirectConflictResolutionPipeline<TId>(syncedTree, propagationTree, deleteConflictNameFactory);
        _nameClashConflictResolution = new NameClashConflictResolutionPipeline<TId>(propagationTree, nameClashConflictNameFactory);
        _moveConflictResolution = new MoveConflictResolutionPipeline<TId>();
        _editConflictResolution = new EditConflictResolutionPipeline<TId>();
        _modelMerge = new ModelMergePipeline<TId>(syncedTree);
        _deleteConflictResolution = new DeleteConflictResolutionPipeline<TId>(syncedTree, propagationTree, deleteConflictNameFactory);
        _statusAdjustment = new StatusAdjustmentPipeline<TId>(syncedTree, remoteUpdateTree, localUpdateTree, _propagationTree);
        _applyToPropagationTree = new ApplyToPropagationTreePipeline<TId>(syncedTree, propagationTree);

        _syncedTreeTraversal =
            new ActiveTreeTraversal<SyncedTree<TId>, SyncedTreeNode<TId>, SyncedTreeNodeModel<TId>, TId>();
        _propagationTreeTraversal =
            new PassiveTreeTraversal<PropagationTree<TId>, PropagationTreeNode<TId>, PropagationTreeNodeModel<TId>, TId>();
    }

    public void Execute(
        UpdateTreeNode<TId>? remoteNode,
        UpdateTreeNode<TId>? localNode)
    {
        if ((remoteNode == null || remoteNode.Model.Status == UpdateStatus.Unchanged) &&
            (localNode == null || localNode.Model.Status == UpdateStatus.Unchanged))
        {
            return;
        }

        ClearProcessedNodes();

        Reconcile(remoteNode, localNode);
    }

    private void Reconcile(
        UpdateTreeNode<TId>? remoteNode,
        UpdateTreeNode<TId>? localNode)
    {
        var (remoteNodeModel, localNodeModel) = PreparedNodeModels(remoteNode, localNode);

        if (!AddProcessedNode(localNodeModel))
        {
            return;
        }

        var previousPropagationNodeModel = _propagationTree.NodeByIdOrDefault(localNodeModel.Id)?.Model.Copy();

        Reconcile(remoteNodeModel, localNodeModel);

        // Remote and local Update Tree nodes might get deleted as a result of pseudo conflict resolution.
        ProcessDirectoryDeletion(
            remoteNode == null || remoteNode.IsDeleted ? null : remoteNode,
            localNode == null || localNode.IsDeleted ? null : localNode,
            previousPropagationNodeModel);
    }

    private void Reconcile(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel)
    {
        (remoteNodeModel, localNodeModel) = ResolvePseudoConflicts(remoteNodeModel, localNodeModel);

        (remoteNodeModel, localNodeModel) = ResolveIndirectConflicts(remoteNodeModel, localNodeModel);

        (remoteNodeModel, localNodeModel) = ResolveMoveConflicts(remoteNodeModel, localNodeModel);

        bool backup;
        (remoteNodeModel, localNodeModel, backup) = ResolveEditConflicts(remoteNodeModel, localNodeModel);

        var propagationNodeModel = Merged(remoteNodeModel, localNodeModel, backup);

        propagationNodeModel = ResolveDeleteConflicts(propagationNodeModel);

        propagationNodeModel = ResolveNameClashConflicts(propagationNodeModel);

        propagationNodeModel = AdjustStatus(propagationNodeModel);

        Apply(propagationNodeModel);
    }

    private void ClearProcessedNodes()
    {
        _processedNodeIds.Clear();
    }

    private bool AddProcessedNode(UpdateTreeNodeModel<TId> nodeModel)
    {
        return _processedNodeIds.Add(nodeModel.Id);
    }

    private (UpdateTreeNodeModel<TId> Remote, UpdateTreeNodeModel<TId> Local) PreparedNodeModels(
        UpdateTreeNode<TId>? remoteNode,
        UpdateTreeNode<TId>? localNode)
    {
        return _preparationPipeline.Execute(remoteNode, localNode);
    }

    private (UpdateTreeNodeModel<TId> Remote, UpdateTreeNodeModel<TId> Local) ResolvePseudoConflicts(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel)
    {
        (remoteNodeModel, localNodeModel) = ResolveCreateCreatePseudoConflicts(remoteNodeModel, localNodeModel);

        /* At the same time nodes can participate in at most two pseudo conflicts:
           Edit-Edit and Move-Move. */

        foreach (var (conflictType, conflictingStatus) in _pseudoConflictDetection.PseudoConflict(
                     remoteNodeModel, localNodeModel))
        {
            // The Delete-Delete (Pseudo) and Move-Move (Pseudo) conflicts are resolved during the Propagation.
            // The descendant node might have been moved outside of the deleted branch and therefore should be
            // processed before ancestor deletion.
            // The parent nodes might have Created status and therefore not yet exist in the Synced Tree.
            if (conflictType is ConflictType.DeleteDeletePseudo or ConflictType.MoveMovePseudo)
            {
                continue;
            }

            (remoteNodeModel, localNodeModel) =
                _pseudoConflictResolution.Execute(remoteNodeModel, localNodeModel, conflictType, conflictingStatus);
        }

        return (remoteNodeModel, localNodeModel);
    }

    private (UpdateTreeNodeModel<TId> Remote, UpdateTreeNodeModel<TId> Local) ResolveCreateCreatePseudoConflicts(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel)
    {
        var (conflictType, conflictingNodeModel) = _pseudoConflictDetection.CreateCreatePseudoConflict(
            remoteNodeModel, localNodeModel);

        if (conflictType == ConflictType.None)
        {
            return (remoteNodeModel, localNodeModel);
        }

        return _pseudoConflictResolution.Execute(
            remoteNodeModel.Status == UpdateStatus.Created ? remoteNodeModel : conflictingNodeModel!,
            localNodeModel.Status == UpdateStatus.Created ? localNodeModel : conflictingNodeModel!,
            conflictType,
            UpdateStatus.Created);
    }

    private (UpdateTreeNodeModel<TId> Remote, UpdateTreeNodeModel<TId> Local) ResolveIndirectConflicts(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel)
    {
        var conflictType = _conflictDetection.IndirectConflict(remoteNodeModel);
        remoteNodeModel = _indirectConflictResolution.Execute(remoteNodeModel, conflictType);

        conflictType = _conflictDetection.IndirectConflict(localNodeModel);
        localNodeModel = _indirectConflictResolution.Execute(localNodeModel, conflictType);

        return (remoteNodeModel, localNodeModel);
    }

    private (UpdateTreeNodeModel<TId> Remote, UpdateTreeNodeModel<TId> Local) ResolveMoveConflicts(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel)
    {
        var conflictType = _conflictDetection.MoveConflict(remoteNodeModel, localNodeModel);
        (remoteNodeModel, localNodeModel) =
            _moveConflictResolution.Execute(remoteNodeModel, localNodeModel, conflictType);

        return (remoteNodeModel, localNodeModel);
    }

    private (UpdateTreeNodeModel<TId> Remote, UpdateTreeNodeModel<TId> Local, bool Backup) ResolveEditConflicts(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel)
    {
        var conflictType = _conflictDetection.EditConflict(remoteNodeModel, localNodeModel);

        return _editConflictResolution.Execute(remoteNodeModel, localNodeModel, conflictType);
    }

    private PropagationTreeNodeModel<TId> ResolveNameClashConflicts(
        PropagationTreeNodeModel<TId> nodeModel)
    {
        var (conflictType, otherNodeModel) = _conflictDetection.NameClashConflict(nodeModel);

        return _nameClashConflictResolution.Execute(nodeModel, otherNodeModel, conflictType);
    }

    private PropagationTreeNodeModel<TId> Merged(
        UpdateTreeNodeModel<TId> remoteNodeModel,
        UpdateTreeNodeModel<TId> localNodeModel,
        bool backup)
    {
        return _modelMerge.Merged(remoteNodeModel, localNodeModel, backup);
    }

    private PropagationTreeNodeModel<TId> ResolveDeleteConflicts(
        PropagationTreeNodeModel<TId> nodeModel)
    {
        var conflictType = _conflictDetection.DeleteConflict(nodeModel);

        return _deleteConflictResolution.Execute(nodeModel, conflictType);
    }

    private PropagationTreeNodeModel<TId> AdjustStatus(PropagationTreeNodeModel<TId> nodeModel)
    {
        return _statusAdjustment.Execute(nodeModel);
    }

    private void Apply(PropagationTreeNodeModel<TId> nodeModel)
    {
        _applyToPropagationTree.Execute(nodeModel);
    }

    private void ProcessDirectoryDeletion(
        UpdateTreeNode<TId>? remoteNode,
        UpdateTreeNode<TId>? localNode,
        PropagationTreeNodeModel<TId>? previousPropagationNodeModel)
    {
        if (_processingFolderDeletion)
        {
            return;
        }

        try
        {
            _processingFolderDeletion = true;

            // The Deleted Propagation Tree node should have no child nodes.
            // When Propagation Tree node changes status to Deleted, reconciliation is
            // repeated for all its child nodes.
            if (previousPropagationNodeModel != null &&
                !previousPropagationNodeModel.RemoteStatus.Contains(UpdateStatus.Deleted) &&
                !previousPropagationNodeModel.LocalStatus.Contains(UpdateStatus.Deleted))
            {
                var node = _propagationTree.NodeByIdOrDefault(previousPropagationNodeModel.Id);
                if (node != null &&
                    (node.Model.RemoteStatus.Contains(UpdateStatus.Deleted) ||
                     node.Model.LocalStatus.Contains(UpdateStatus.Deleted)))
                {
                    ProcessChildren(node);
                }
            }

            // If folder was already deleted, there is no need to further process children
            //if (previousPropagationNodeModel != null && (
            //    previousPropagationNodeModel.RemoteStatus.Contains(UpdateStatus.Deleted) ||
            //    previousPropagationNodeModel.LocalStatus.Contains(UpdateStatus.Deleted)))
            //{
            //    return;
            //}

            // Only directly deleted directories requires repeating reconciliation
            // for all Synced Tree children.
            if (remoteNode?.Model.Status.Contains(UpdateStatus.Deleted) == true)
            {
                ProcessChildren(remoteNode.Model, Replica.Remote);
            }
            else if (localNode?.Model.Status.Contains(UpdateStatus.Deleted) == true)
            {
                ProcessChildren(localNode.Model, Replica.Local);
            }
        }
        finally
        {
            _processingFolderDeletion = false;
        }
    }

    private void ProcessChildren(PropagationTreeNode<TId>? node)
    {
        if (node == null || node.IsLeaf)
        {
            return;
        }

        foreach (var child in _propagationTreeTraversal.ExcludeStartingNode().PreOrder(node))
        {
            Reconcile(child.Model);
        }
    }

    private void ProcessChildren(UpdateTreeNodeModel<TId> nodeModel, Replica replica)
    {
        // Deleted node should exist in the SyncedTree
        var syncedNode = _syncedTree.NodeByOwnId(nodeModel.Id, replica);

        ProcessChildren(syncedNode, replica);
    }

    private void ProcessChildren(SyncedTreeNode<TId> startingNode, Replica replica)
    {
        ReconcileOtherChildren(startingNode, replica);

        // Directory was deleted on "own" replica
        var ownUpdateTree = replica == Replica.Local ? _localUpdateTree : _remoteUpdateTree;

        _syncedTreeTraversal
            .PreOrder(PreOrder)
            .ExcludeStartingNode()
            .Execute(startingNode);

        return;

        void PreOrder(SyncedTreeNode<TId> node)
        {
            var ownUpdateTreeNode = ownUpdateTree.NodeByIdOrDefault(node.Model.OwnId(replica));

            /* If the node exists in the own Update Tree (of the replica on which the parent was deleted),
            // then this node has survived deletion being moved outside of the deleted branch.
            // This node is still processed as in the Propagation Tree it might be moved back
            // to the deleted branch during the Reconciliation, but its children are skipped
            // from processing. */

            Reconcile(node.Model);
            ReconcileOtherChildren(node, replica);

            // If the node exists in the own Update Tree (of the replica on which the parent was deleted),
            // then this node has survived deletion being moved outside of the deleted branch.
            // Therefore, its children are skipped from processing.
            if (ownUpdateTreeNode != null)
            {
                _syncedTreeTraversal.SkipChildren();
            }
        }
    }

    private void Reconcile(IAltIdentifiable<TId, TId> nodeModel)
    {
        var remoteNode = _remoteUpdateTree.NodeByIdOrDefault(nodeModel.AltId);
        var localNode = _localUpdateTree.NodeByIdOrDefault(nodeModel.Id);

        if (remoteNode == null && localNode == null)
        {
            return;
        }

        if ((remoteNode != null && remoteNode.Model.Status != UpdateStatus.Unchanged) ||
            (localNode != null && localNode.Model.Status != UpdateStatus.Unchanged))
        {
            Reconcile(remoteNode, localNode);
        }
    }

    private void ReconcileOtherChildren(SyncedTreeNode<TId> node, Replica replica)
    {
        if (node.Type != NodeType.Directory)
        {
            return;
        }

        // Directory was deleted on "own" replica
        var otherUpdateTree = replica == Replica.Local ? _remoteUpdateTree : _localUpdateTree;

        var otherNode = otherUpdateTree.NodeByIdOrDefault(node.Model.OtherId(replica));
        if (otherNode == null || otherNode.IsLeaf)
        {
            return;
        }

        foreach (var otherChild in otherNode.Children)
        {
            if (replica == Replica.Local)
            {
                Reconcile(otherChild, null);
            }
            else
            {
                Reconcile(null, otherChild);
            }
        }
    }
}
