using System.Text;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Engine.Shared;
using Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Update;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem.Traversal;

namespace Proton.Drive.Sdk.Sync.Engine.Consolidation;

internal sealed class UpdateStatisticsCalculator<TId>
    where TId : IEquatable<TId>, IComparable<TId>
{
    private static readonly UpdateStatus[] UpdateTypes =
    [
        UpdateStatus.Created,
        UpdateStatus.Edited,
        UpdateStatus.Renamed,
        UpdateStatus.Moved,
        UpdateStatus.Deleted,
    ];

    private readonly Replica _replica;
    private readonly UpdateTree<TId> _updateTree;
    private readonly ILogger<ConsolidationPipeline<TId>> _logger;

    private readonly PassiveTreeTraversal<UpdateTree<TId>, UpdateTreeNode<TId>, UpdateTreeNodeModel<TId>, TId> _updateTreeTraversal = new();

    public UpdateStatisticsCalculator(
        Replica replica,
        UpdateTree<TId> updateTree,
        ILogger<ConsolidationPipeline<TId>> logger)
    {
        _replica = replica;
        _updateTree = updateTree;
        _logger = logger;
    }

    public void Execute(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("Started calculating {Replica} update statistics", _replica);

        foreach (var syncRootNode in _updateTree.Root.Children)
        {
            CalculateSyncRootUpdateStatistics(syncRootNode, cancellationToken);
        }

        _logger.LogDebug("Finished calculating {Replica} update statistics", _replica);
    }

    private void CalculateSyncRootUpdateStatistics(UpdateTreeNode<TId> syncRootNode, CancellationToken cancellationToken)
    {
        var updates = new Dictionary<(UpdateStatus UpdateType, NodeType NodeType), int>();

        foreach (var node in _updateTreeTraversal.IncludeStartingNode().PreOrder(syncRootNode, cancellationToken))
        {
            if ((node.Model.Status & UpdateStatus.All) == default)
            {
                continue;
            }

            foreach (var updateType in UpdateTypes)
            {
                if (!node.Model.Status.HasFlag(updateType))
                {
                    continue;
                }

                var key = (updateType, node.Type);
                updates[key] = updates.GetValueOrDefault(key) + 1;
            }
        }

        if (updates.Count == 0)
        {
            return;
        }

        var rootId = int.Parse(syncRootNode.Name);
        LogStatistics(rootId, updates);
    }

    private void LogStatistics(int rootId, Dictionary<(UpdateStatus UpdateType, NodeType NodeType), int> updates)
    {
        var messageBuilder = new StringBuilder();
        messageBuilder.Append($"Root {rootId}:");
        var nextUpdateTypeEntry = false;

        foreach (var updateType in Enum.GetValues<UpdateStatus>())
        {
            var numberOfFiles = updates.GetValueOrDefault((updateType, NodeType.File));
            var numberOfFolders = updates.GetValueOrDefault((updateType, NodeType.Directory));

            if (numberOfFiles == 0 && numberOfFolders == 0)
            {
                continue;
            }

            if (nextUpdateTypeEntry)
            {
                messageBuilder.Append(';');
            }

            messageBuilder.Append($" {updateType} ");
            nextUpdateTypeEntry = true;

            if (numberOfFiles != 0)
            {
                messageBuilder.Append($"{numberOfFiles} files");
            }

            if (numberOfFiles != 0 && numberOfFolders != 0)
            {
                messageBuilder.Append(", ");
            }

            if (numberOfFolders != 0)
            {
                messageBuilder.Append($"{numberOfFolders} folders");
            }
        }

        _logger.LogInformation(messageBuilder.ToString());
    }
}
