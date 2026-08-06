using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Adapter.Shared;
using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.Adapter.UpdateDetection.StateBased.Enumeration;
using Proton.Drive.Sdk.Sync.Shared.Adapters;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Logging;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Adapter;

internal sealed class FileRevisionProvider<TId, TAltId> : IFileRevisionProvider<TId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<FileRevisionProvider<TId, TAltId>> _logger;
    private readonly IScheduler _syncScheduler;
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly IFileSystemClient<TAltId> _fileSystemClient;
    private readonly IReadOnlyDictionary<TId, RootInfo<TAltId>> _syncRoots;
    private readonly EnumerationFailureStep<TId, TAltId> _failureStep;
    private readonly TimeSpan _minDelayBeforeFileUpload;

    public FileRevisionProvider(
        IScheduler syncScheduler,
        AdapterTree<TId, TAltId> adapterTree,
        IFileSystemClient<TAltId> fileSystemClient,
        IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots,
        EnumerationFailureStep<TId, TAltId> failureStep,
        TimeSpan minDelayBeforeFileUpload,
        ILogger<FileRevisionProvider<TId, TAltId>> logger)
    {
        _logger = logger;
        _syncScheduler = syncScheduler;
        _adapterTree = adapterTree;
        _fileSystemClient = fileSystemClient;
        _syncRoots = syncRoots;
        _failureStep = failureStep;
        _minDelayBeforeFileUpload = minDelayBeforeFileUpload;
    }

    public async Task<ISourceRevision> OpenFileForReadingAsync(TId id, long contentVersion, CancellationToken cancellationToken)
    {
        var (fileInfo, initialNodeModel) = await Schedule(() => Prepare(id, contentVersion), cancellationToken).ConfigureAwait(false);

        var pathToLog = _logger.GetSensitiveValueForLogging(fileInfo.Path);
        _logger.LogInformation(
            "Reading the file \"{Path}\" \"{Root}\"/{Id} {ExternalId}, ContentVersion={ContentVersion}, Size={Size} KiB",
            pathToLog,
            fileInfo.Root?.Id,
            id,
            fileInfo.GetCompoundId(),
            contentVersion,
            (fileInfo.Size + 1023) / 1024);

        try
        {
            return await _fileSystemClient.OpenFileForReadingAsync(fileInfo, cancellationToken).ConfigureAwait(false);
        }
        catch (FileSystemClientException ex)
        {
            await Schedule(() => HandleFailure(ex, initialNodeModel), cancellationToken).ConfigureAwait(false);

            throw new FileRevisionProviderException(
                $"Reading the file \"{fileInfo.Root?.Id}\"/{id} {fileInfo.GetCompoundId()} failed: {ex.CombinedMessage()}",
                ex.ErrorCode,
                ex);
        }
    }

    private (NodeInfo<TAltId> NodeInfo, AdapterTreeNodeModel<TId, TAltId> NodeModel) Prepare(TId nodeId, long requestedVersion)
    {
        var node = _adapterTree.NodeByIdOrDefault(nodeId) ?? throw new FileRevisionProviderException(
            $"Adapter Tree node with Id={nodeId} does not exist",
            FileSystemErrorCode.ObjectNotFound);

        ValidatePreconditions(node, requestedVersion);

        return (ToNodeInfo(node), node.Model);
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

        if (node.Model.ContentHasChangedRecently(_minDelayBeforeFileUpload))
        {
            throw new FileRevisionProviderException(
                $"File with Id={node.Id} has been recently modified",
                FileSystemErrorCode.LastWriteTimeTooRecent);
        }
    }

    private void HandleFailure(Exception exception, AdapterTreeNodeModel<TId, TAltId> initialNodeModel)
    {
        _failureStep.Execute(exception, initialNodeModel);
    }

    private NodeInfo<TAltId> ToNodeInfo(AdapterTreeNode<TId, TAltId> node)
    {
        return node.ToNodeInfo(_syncRoots);
    }

    private async Task Schedule(Action origin, CancellationToken cancellationToken)
    {
        using (await _syncScheduler.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            origin.Invoke();
        }
    }

    private async Task<T> Schedule<T>(Func<T> origin, CancellationToken cancellationToken)
    {
        using (await _syncScheduler.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            return origin.Invoke();
        }
    }
}
