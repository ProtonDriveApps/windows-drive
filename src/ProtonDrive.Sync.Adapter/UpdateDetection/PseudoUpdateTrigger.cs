using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter.UpdateDetection;

/// <summary>
/// Provides capability to trigger pseudo update detection for specific nodes.
/// </summary>
internal sealed class PseudoUpdateTrigger<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ITransactedScheduler _syncScheduler;
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly IReadOnlyDictionary<TId, RootInfo<TAltId>> _syncRoots;
    private readonly IIdentitySource<long> _contentVersionSequence;
    private readonly NodeUpdateDetection<TId, TAltId> _nodeUpdateDetection;
    private readonly ILogger<PseudoUpdateTrigger<TId, TAltId>> _logger;

    public PseudoUpdateTrigger(
        ITransactedScheduler syncScheduler,
        AdapterTree<TId, TAltId> adapterTree,
        IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots,
        IIdentitySource<long> contentVersionSequence,
        NodeUpdateDetection<TId, TAltId> nodeUpdateDetection,
        ILogger<PseudoUpdateTrigger<TId, TAltId>> logger)
    {
        _syncScheduler = syncScheduler;
        _adapterTree = adapterTree;
        _syncRoots = syncRoots;
        _contentVersionSequence = contentVersionSequence;
        _nodeUpdateDetection = nodeUpdateDetection;
        _logger = logger;
    }

    public Task TriggerPseudoFileEditAsync(TId id, long requestedVersion, CancellationToken cancellationToken)
    {
        return ScheduleAndCommit(() => TriggerPseudoFileEdit(id, requestedVersion), cancellationToken);
    }

    private void TriggerPseudoFileEdit(TId id, long requestedVersion)
    {
        var node = Prepare(id, requestedVersion);

        var nameToLog = _logger.GetSensitiveValueForLogging(node.Name);
        _logger.LogInformation(
            "Triggering pseudo edit of file \"{Name}\" \"{Root}\"/{Id} {ExternalId}, ContentVersion={ContentVersion}",
            nameToLog,
            _syncRoots[node.GetSyncRoot().Id].Id,
            id,
            node.Model.AltId,
            node.Model.ContentVersion);

        // Changing content version triggers a file edit to be detected
        var newContentVersion = _contentVersionSequence.NextValue();

        var incomingModel = IncomingAdapterTreeNodeModel<TId, TAltId>
            .FromNodeModel(node.Model)
            .WithContentVersion(newContentVersion);

        _nodeUpdateDetection.Execute(node, incomingModel);
    }

    private AdapterTreeNode<TId, TAltId> Prepare(TId nodeId, long requestedVersion)
    {
        var node = _adapterTree.NodeByIdOrDefault(nodeId) ?? throw new FileRevisionProviderException(
            $"Adapter Tree node with Id={nodeId} does not exist",
            FileSystemErrorCode.ObjectNotFound);

        ValidatePreconditions(node, requestedVersion);

        return node;
    }

    private void ValidatePreconditions(AdapterTreeNode<TId, TAltId> node, long requestedVersion)
    {
        if (node.Type != NodeType.File)
        {
            throw new FileRevisionProviderException(
                $"Adapter Tree node with Id={node.Id} is not a file",
                FileRevisionProviderErrorCode.NotAFile);
        }

        var syncRoot = _syncRoots[node.GetSyncRoot().Id];
        if (!syncRoot.IsEnabled)
        {
            throw new FileRevisionProviderException(
                $"Adapter Tree node with Id={node.Id} is in a disabled root with Id={syncRoot.Id}",
                FileRevisionProviderErrorCode.RootDisabled);
        }

        if (node.Model.IsDirtyPlaceholder())
        {
            throw new InvalidOperationException($"Requested file content source of the node with Id={node.Id} that is a dirty placeholder");
        }

        if (node.IsNodeOrBranchDeleted())
        {
            throw new FileRevisionProviderException(
                $"Adapter Tree node with Id={node.Id} or branch is deleted",
                FileRevisionProviderErrorCode.NodeOrBranchDeleted);
        }

        if (node.Model.ContentVersion != requestedVersion)
        {
            throw new FileRevisionProviderException(
                $"File with Id={node.Id} content version has diverged from expected {requestedVersion} to {node.Model.ContentVersion}",
                FileRevisionProviderErrorCode.ContentVersionDiverged);
        }
    }

    private async Task ScheduleAndCommit(Action origin, CancellationToken cancellationToken)
    {
        using (await _syncScheduler.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            _syncScheduler.ForceCommit = true;

            origin.Invoke();
        }
    }
}
