using System;
using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Engine.Consolidation;

internal sealed class StateConsistencyGuard<TId>
    where TId : IEquatable<TId>, IComparable<TId>
{
    private readonly Replica _replica;
    private readonly SyncedTree<TId> _syncedTree;
    private readonly UpdateTree<TId> _updateTree;
    private readonly ILogger<ConsolidationPipeline<TId>> _logger;

    public StateConsistencyGuard(
        Replica replica,
        SyncedTree<TId> syncedTree,
        UpdateTree<TId> updateTree,
        ILogger<ConsolidationPipeline<TId>> logger)
    {
        _replica = replica;
        _syncedTree = syncedTree;
        _updateTree = updateTree;
        _logger = logger;
    }

    public bool IsConsistent(
        Operation<FileSystemNodeModel<TId>> detectedUpdate,
        UpdateTreeNode<TId>? node,
        SyncedTreeNode<TId>? syncedNode)
    {
        var nodeExists = node is not null || syncedNode is not null;
        var parentNodeExists = detectedUpdate.Type is not OperationType.Create and not OperationType.Move
                               || _updateTree.NodeByIdOrDefault(detectedUpdate.Model.ParentId) is not null
                               || _syncedTree.NodeByOwnIdOrDefault(detectedUpdate.Model.ParentId, _replica) is not null;

        switch (detectedUpdate.Type)
        {
            // Creation at non existing parent
            case OperationType.Create when !parentNodeExists:

            // Editing in a deleted branch
            case OperationType.Edit when !nodeExists:

            // Deletion in a deleted branch
            case OperationType.Delete when !nodeExists:

            // Move inside a deleted branch or between deleted branches,
            // also move out of deleted branch and move into a deleted branch
            case OperationType.Move when !nodeExists || !parentNodeExists:

                LogInconsistency(detectedUpdate);

                return false;
        }

        return true;
    }

    private void LogInconsistency(Operation<FileSystemNodeModel<TId>> detectedUpdate)
    {
        _logger.LogWarning(
            "Inconsistent detected update: {Replica} {OperationType} {Id} at parent {ParentId}",
            _replica,
            detectedUpdate.Type,
            detectedUpdate.Model.Id,
            detectedUpdate.Model.ParentId);
    }
}
