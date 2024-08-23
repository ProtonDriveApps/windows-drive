using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Adapter.Trees.Dirty;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Adapter.UpdateDetection.StateBased;

internal sealed class DeletionDetection<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<DeletionDetection<TId, TAltId>> _logger;
    private readonly ITransactedScheduler _syncScheduler;
    private readonly IReadOnlyDictionary<TId, RootInfo<TAltId>> _syncRoots;
    private readonly NodeUpdateDetection<TId, TAltId> _nodeUpdateDetection;

    private readonly DirtyNodesTraversal<TId, TAltId> _dirtyNodesTraversal;

    public DeletionDetection(
        ILogger<DeletionDetection<TId, TAltId>> logger,
        ITransactedScheduler syncScheduler,
        AdapterTree<TId, TAltId> adapterTree,
        DirtyTree<TId> dirtyTree,
        IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots,
        NodeUpdateDetection<TId, TAltId> nodeUpdateDetection)
    {
        _logger = logger;
        _syncScheduler = syncScheduler;
        _syncRoots = syncRoots;
        _nodeUpdateDetection = nodeUpdateDetection;

        _dirtyNodesTraversal = new DirtyNodesTraversal<TId, TAltId>(adapterTree, dirtyTree);
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        foreach (var volumeSyncRoots in GetEnabledSyncRootsGroupedByVolume())
        {
            await ScheduleAndCommit(() => DetectDeletions(volumeSyncRoots, cancellationToken)).ConfigureAwait(false);
        }
    }

    private IEnumerable<IReadOnlyCollection<(TId NodeId, RootInfo<TAltId> Root)>> GetEnabledSyncRootsGroupedByVolume()
    {
        return _syncRoots
            .Where(r => r.Value.IsEnabled)
            .GroupBy(r => r.Value.VolumeId)
            .Select(g => g.Select(r => (r.Key, r.Value)).ToList().AsReadOnly());
    }

    private void DetectDeletions(IReadOnlyCollection<(TId NodeId, RootInfo<TAltId> Root)> syncRoots, CancellationToken cancellationToken)
    {
        var volumeId = syncRoots.Select(x => x.Root.VolumeId).FirstOrDefault();

        if (HasBranchesToEnumerate(syncRoots, cancellationToken))
        {
            _logger.LogInformation("There are dirty branches left on volume with Id={VolumeId}, skipping deletion of lost nodes", volumeId);

            return;
        }

        _logger.LogDebug("There are no dirty branches left on volume with Id={VolumeId}, deleting lost nodes", volumeId);

        DeleteNodes(syncRoots, cancellationToken);
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

            _logger.LogInformation("The Adapter Tree node with Id={Id} has Status=({Status})", node.Id, node.Model.Status);

            return true;
        }

        return false;
    }

    private void DeleteNodes(IEnumerable<(TId NodeId, RootInfo<TAltId> Root)> syncRoots, CancellationToken cancellationToken)
    {
        var nodes = syncRoots.SelectMany(syncRoot => NodesToDelete(syncRoot.NodeId, cancellationToken));
        foreach (var node in nodes)
        {
            DetectNodeUpdate(node, null);
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
