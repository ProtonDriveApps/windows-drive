using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Adapter.Shared;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Adapter;

internal sealed class FileRevisionProvider<TId, TAltId> : IFileRevisionProvider<TId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<FileRevisionProvider<TId, TAltId>> _logger;
    private readonly IScheduler _syncScheduler;
    private readonly AdapterTree<TId, TAltId> _adapterTree;
    private readonly IFileSystemClient<TAltId> _fileSystemClient;
    private readonly IReadOnlyDictionary<TId, RootInfo<TAltId>> _syncRoots;
    private readonly TimeSpan _minDelayBeforeFileUpload;

    public FileRevisionProvider(
        IScheduler syncScheduler,
        AdapterTree<TId, TAltId> adapterTree,
        IFileSystemClient<TAltId> fileSystemClient,
        IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots,
        TimeSpan minDelayBeforeFileUpload,
        ILogger<FileRevisionProvider<TId, TAltId>> logger)
    {
        _logger = logger;
        _syncScheduler = syncScheduler;
        _adapterTree = adapterTree;
        _fileSystemClient = fileSystemClient;
        _syncRoots = syncRoots;
        _minDelayBeforeFileUpload = minDelayBeforeFileUpload;
    }

    public async Task<IRevision> OpenFileForReadingAsync(TId id, long contentVersion, CancellationToken cancellationToken)
    {
        var fileInfo = await Schedule(() => Prepare(id, contentVersion)).ConfigureAwait(false);

        var pathToLog = _logger.GetSensitiveValueForLogging(fileInfo.Path);
        _logger.LogInformation(
            "Reading the file \"{Path}\" \"{Root}\"/{Id} {ExternalId}, ContentVersion={ContentVersion}",
            pathToLog,
            fileInfo.Root?.Id,
            id,
            fileInfo.GetCompoundId(),
            contentVersion);

        try
        {
            return await _fileSystemClient.OpenFileForReading(fileInfo, cancellationToken).ConfigureAwait(false);
        }
        catch (FileSystemClientException ex)
        {
            throw new FileRevisionProviderException(
                $"Reading the file \"{fileInfo.Root?.Id}\"/{id} {fileInfo.GetCompoundId()} failed: {ex.CombinedMessage()}",
                ex.ErrorCode,
                ex);
        }
    }

    private NodeInfo<TAltId> Prepare(TId nodeId, long requestedVersion)
    {
        var node = NodeById(nodeId);

        ValidatePreconditions(node, requestedVersion);

        return ToNodeInfo(node);
    }

    private AdapterTreeNode<TId, TAltId> NodeById(TId nodeId)
    {
        return _adapterTree.NodeByIdOrDefault(nodeId)
               ?? throw new FileRevisionProviderException($"Adapter Tree node with Id={nodeId} does not exist", FileSystemErrorCode.ObjectNotFound);
    }

    private void ValidatePreconditions(AdapterTreeNode<TId, TAltId> node, long contentVersion)
    {
        if (node.Type != NodeType.File)
        {
            throw new FileRevisionProviderException($"Adapter Tree node with Id={node.Id} is not a file");
        }

        var syncRoot = _syncRoots[node.GetSyncRoot().Id];
        if (!syncRoot.IsEnabled)
        {
            throw new FileRevisionProviderException($"Adapter Tree node with Id={node.Id} is in a disabled root with Id={syncRoot.Id}");
        }

        if (node.Model.IsDirtyPlaceholder())
        {
            throw new InvalidOperationException($"Requested file content source of the node with Id={node.Id} that is a dirty placeholder");
        }

        if (node.IsNodeOrBranchDeleted())
        {
            throw new FileRevisionProviderException($"Adapter Tree node with Id={node.Id} or branch is deleted");
        }

        if (node.Model.ContentVersion != contentVersion)
        {
            throw new FileRevisionProviderException(
                $"File with Id={node.Id} content version has diverged from expected {contentVersion} to {node.Model.ContentVersion}");
        }

        if (node.Model.ContentHasChangedRecently(_minDelayBeforeFileUpload))
        {
            throw new FileRevisionProviderException(
                $"File with Id={node.Id} has been recently modified",
                FileSystemErrorCode.LastWriteTimeTooRecent);
        }
    }

    private NodeInfo<TAltId> ToNodeInfo(AdapterTreeNode<TId, TAltId> node)
    {
        return node.ToNodeInfo(_syncRoots);
    }

    private Task<T> Schedule<T>(Func<T> origin)
    {
        return _syncScheduler.Schedule(origin);
    }
}
