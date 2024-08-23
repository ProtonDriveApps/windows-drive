using System;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Trees.FileSystem.Traversal;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.UpdateDetection.StateBased.Enumeration;

/// <summary>
/// Migrates Adapter Tree branch to a specific volume
/// </summary>
internal sealed class RootMigrationStep<TId, TAltId>
where TId : IEquatable<TId>
where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<RootMigrationStep<TId, TAltId>> _logger;
    private readonly AdapterTree<TId, TAltId> _adapterTree;

    private readonly PassiveTreeTraversal<AdapterTree<TId, TAltId>, AdapterTreeNode<TId, TAltId>, AdapterTreeNodeModel<TId, TAltId>, TId>
        _treeTraversal = new();

    public RootMigrationStep(
        ILogger<RootMigrationStep<TId, TAltId>> logger,
        AdapterTree<TId, TAltId> adapterTree)
    {
        _logger = logger;
        _adapterTree = adapterTree;
    }

    public void MigrateSyncRootToVolume(AdapterTreeNode<TId, TAltId> syncRootNode, int volumeId)
    {
        _logger.LogInformation("Migrating sync root {Root} to volume {VolumeId}", syncRootNode.Name, volumeId);

        var branch = _treeTraversal.IncludeStartingNode().PreOrder(syncRootNode);

        foreach (var node in branch)
        {
            var nodeModel = node.Model.Copy().WithAltId((volumeId, node.Model.AltId.ItemId));

            Update(nodeModel);
        }
    }

    private void Update(AdapterTreeNodeModel<TId, TAltId> nodeModel)
    {
        _adapterTree.Operations.Execute(
            new Operation<AdapterTreeNodeModel<TId, TAltId>>(
                OperationType.Update,
                nodeModel));
    }
}
