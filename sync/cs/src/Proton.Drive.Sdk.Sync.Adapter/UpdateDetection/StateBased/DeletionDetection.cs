using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.Adapter.Trees.Dirty;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.Reporting;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection.StateBased;

internal sealed class DeletionDetection<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<DeletionDetection<TId, TAltId>> _logger;
    private readonly Replica _replica;
    private readonly ITransactedScheduler _syncScheduler;
    private readonly IReadOnlyDictionary<TId, RootInfo<TAltId>> _syncRoots;
    private readonly NodeUpdateDetection<TId, TAltId> _nodeUpdateDetection;
    private readonly IErrorReporting _errorReporting;
    private readonly DirtyNodesTraversal<TId, TAltId> _dirtyNodesTraversal;

    public DeletionDetection(
        ILogger<DeletionDetection<TId, TAltId>> logger,
        Replica replica,
        ITransactedScheduler syncScheduler,
        AdapterTree<TId, TAltId> adapterTree,
        DirtyTree<TId> dirtyTree,
        IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots,
        NodeUpdateDetection<TId, TAltId> nodeUpdateDetection,
        IErrorReporting errorReporting)
    {
        _logger = logger;
        _replica = replica;
        _syncScheduler = syncScheduler;
        _syncRoots = syncRoots;
        _nodeUpdateDetection = nodeUpdateDetection;
        _errorReporting = errorReporting;

        _dirtyNodesTraversal = new DirtyNodesTraversal<TId, TAltId>(adapterTree, dirtyTree);
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var numberOfDeletedNodes = 0;

        foreach (var volumeSyncRoots in GetEnabledSyncRootsGroupedByVolume())
        {
            await ScheduleAndCommit(() => numberOfDeletedNodes += DetectDeletions(volumeSyncRoots, cancellationToken)).ConfigureAwait(false);
        }

        return numberOfDeletedNodes;
    }

    private IEnumerable<IReadOnlyCollection<(TId NodeId, RootInfo<TAltId> Root)>> GetEnabledSyncRootsGroupedByVolume()
    {
        return _syncRoots
            .Where(r => r.Value.IsEnabled)
            .GroupBy(r => r.Value.VolumeId)
            .Select(g => g.Select(r => (r.Key, r.Value)).ToList().AsReadOnly());
    }

    private int DetectDeletions(IReadOnlyCollection<(TId NodeId, RootInfo<TAltId> Root)> syncRoots, CancellationToken cancellationToken)
    {
        var volumeId = syncRoots.Select(x => x.Root.VolumeId).FirstOrDefault();

        if (HasBranchesToEnumerate(syncRoots, cancellationToken))
        {
            _logger.LogInformation(
                "There are dirty branches left on {Replica} adapter on volume with Id={VolumeId}, skipping deletion of lost nodes",
                _replica,
                volumeId);

            return 0;
        }

        _logger.LogDebug("There are no dirty branches left on {Replica} adapter on volume with Id={VolumeId}, deleting lost nodes", _replica, volumeId);

        return DeleteNodes(syncRoots, cancellationToken);
    }

    private bool HasBranchesToEnumerate(IEnumerable<(TId NodeId, RootInfo<TAltId> Root)> syncRoots, CancellationToken cancellationToken)
    {
        foreach (var (syncRootNodeId, _) in syncRoots)
        {
            // Traversing one sync root in Dirty Tree to see if there are nodes to enumerate
            var node = NodesToEnumerate(syncRootNodeId, cancellationToken).FirstOrDefault();

            if (node == null)
            {
                continue;
            }

            _logger.LogInformation("The {Replica} Adapter Tree node with Id={Id} has Status=({Status})", _replica, node.Id, node.Model.Status);

            return true;
        }

        return false;
    }

    private int DeleteNodes(IEnumerable<(TId NodeId, RootInfo<TAltId> Root)> syncRoots, CancellationToken cancellationToken)
    {
        var totalDeletedNodeCount = 0;

        foreach (var syncRoot in syncRoots)
        {
            var nodes = NodesToDelete(syncRoot.NodeId, cancellationToken);
            var deletedNodeCount = 0;

            foreach (var node in nodes)
            {
                DetectNodeUpdate(node, null);
                deletedNodeCount++;
            }

            LogDetectedDeletions(syncRoot.Root, deletedNodeCount);

            totalDeletedNodeCount += deletedNodeCount;
        }

        return totalDeletedNodeCount;
    }

    private void LogDetectedDeletions(RootInfo<TAltId> root, int deletedNodeCount)
    {
        const int bulkDeletionWarningThreshold = 1000;

        if (deletedNodeCount == 0)
        {
            return;
        }

        var isBulkDeletion = deletedNodeCount >= bulkDeletionWarningThreshold;

        if (isBulkDeletion)
        {
            _logger.LogWarning(
                "{Replica} adapter detected {DeletedCount} deletions on root {RootId}",
                _replica,
                deletedNodeCount,
                root.Id);

            _errorReporting.CaptureWarning($"{_replica} adapter detected a bulk deletion on root {root.Id}: {deletedNodeCount} deletions");
        }
        else
        {
            _logger.LogInformation(
                "{Replica} adapter detected {DeletedCount} deletions on root {RootId}",
                _replica,
                deletedNodeCount,
                root.Id);
        }
    }

    private IEnumerable<AdapterTreeNode<TId, TAltId>> NodesToEnumerate(TId startingNodeId, CancellationToken cancellationToken)
    {
        return _dirtyNodesTraversal.DirtyNodes(startingNodeId, cancellationToken);
    }

    private IEnumerable<AdapterTreeNode<TId, TAltId>> NodesToDelete(TId startingNodeId, CancellationToken cancellationToken)
    {
        return _dirtyNodesTraversal.LostOrDeletedNodes(startingNodeId, cancellationToken);
    }

    private void DetectNodeUpdate(
        AdapterTreeNode<TId, TAltId>? currentNode,
        IncomingAdapterTreeNodeModel<TId, TAltId>? incomingNodeModel)
    {
        _nodeUpdateDetection.Execute(currentNode, incomingNodeModel);
    }

    private Task ScheduleAndCommit(Action origin)
    {
        return _syncScheduler.ScheduleAndCommit(origin);
    }
}
