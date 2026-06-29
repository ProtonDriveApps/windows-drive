using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem;

internal sealed class DispatchingFileSystemClient<TId> : IFileSystemClient<TId>
    where TId : IEquatable<TId>
{
    private readonly IReadOnlyDictionary<RootInfo<TId>, IFileSystemClient<TId>> _rootToClientDictionary;

    public DispatchingFileSystemClient(IReadOnlyDictionary<RootInfo<TId>, IFileSystemClient<TId>> rootToClientDictionary)
    {
        _rootToClientDictionary = rootToClientDictionary;
    }

    public void Connect(string syncRootPath, IFileHydrationDemandHandler<TId> fileHydrationDemandHandler)
    {
        foreach (var (root, fileSystemClient) in _rootToClientDictionary)
        {
            fileSystemClient.Connect(syncRootPath, new RootedFileHydrationDemandHandler(fileHydrationDemandHandler, root));
        }
    }

    public async Task DisconnectAsync()
    {
        await Task.WhenAll(_rootToClientDictionary.Values.Select(fileSystemClient => fileSystemClient.DisconnectAsync())).ConfigureAwait(false);
    }

    public async Task<NodeInfo<TId>> GetInfoAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        var completeInfo = await client.GetInfoAsync(info, cancellationToken).ConfigureAwait(false);

        return AddRoot(completeInfo, info.Root);
    }

    public IAsyncEnumerable<NodeInfo<TId>> EnumerateAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        if (info.IsEmpty)
        {
            return _rootToClientDictionary.Keys
                .Select(root => NodeInfo<TId>.Directory().WithId(root.NodeId).WithName(root.Id.ToString()).WithRoot(root))
                .ToAsyncEnumerable();
        }

        var client = GetClient(info);
        var result = client.EnumerateAsync(info, cancellationToken);

        return result.Select(node => AddRoot(node, info.Root));
    }

    public async Task<NodeInfo<TId>> CreateDirectoryAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        var result = await client.CreateDirectoryAsync(info, cancellationToken).ConfigureAwait(false);

        return AddRoot(result, info.Root);
    }

    public async Task<IDestinationRevision<TId>> CreateFileAsync(
        NodeInfo<TId> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        var result = await client.CreateFileAsync(info, tempFileName, thumbnailProvider, fileMetadataProvider, progressCallback, cancellationToken)
            .ConfigureAwait(false);

        return new DispatchingRevisionCreationProcess(this, result, info.Root!);
    }

    public Task<ISourceRevision> OpenFileForReadingAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        return client.OpenFileForReadingAsync(info, cancellationToken);
    }

    public async Task<IDestinationRevision<TId>> CreateRevisionAsync(
        NodeInfo<TId> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        var result = await client.CreateRevisionAsync(
                info,
                size,
                lastWriteTime,
                tempFileName,
                thumbnailProvider,
                fileMetadataProvider,
                progressCallback,
                cancellationToken)
            .ConfigureAwait(false);

        return new DispatchingRevisionCreationProcess(this, result, info.Root!);
    }

    public Task MoveAsync(IReadOnlyList<NodeInfo<TId>> sourceNodes, NodeInfo<TId> destinationInfo, CancellationToken cancellationToken)
    {
        var client = GetClient(destinationInfo);

        return client.MoveAsync(sourceNodes, destinationInfo, cancellationToken);
    }

    public Task MoveAsync(NodeInfo<TId> info, NodeInfo<TId> destinationInfo, CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        return client.MoveAsync(info, destinationInfo, cancellationToken);
    }

    public Task DeleteAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        return client.DeleteAsync(info, cancellationToken);
    }

    public Task DeletePermanentlyAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        return client.DeletePermanentlyAsync(info, cancellationToken);
    }

    public Task DeleteRevisionAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        return client.DeleteRevisionAsync(info, cancellationToken);
    }

    public void SetInSyncState(NodeInfo<TId> info)
    {
        var client = GetClient(info);

        client.SetInSyncState(info);
    }

    public Task HydrateFileAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        return client.HydrateFileAsync(info, cancellationToken);
    }

    private IFileSystemClient<TId> GetClient(NodeInfo<TId> info)
    {
        if (info.Root is null)
        {
            throw new InvalidOperationException("Invalid operation for root");
        }

        if (!_rootToClientDictionary.TryGetValue(info.Root, out var client))
        {
            throw new FileSystemClientException("Unknown root");
        }

        return client;
    }

    private NodeInfo<TId> AddRoot(NodeInfo<TId> nodeInfo, RootInfo<TId>? root)
    {
        return nodeInfo.Copy().WithRoot(root);
    }

    private class DispatchingRevisionCreationProcess : IDestinationRevision<TId>
    {
        private readonly DispatchingFileSystemClient<TId> _owner;
        private readonly IDestinationRevision<TId> _origin;
        private readonly RootInfo<TId> _root;

        public DispatchingRevisionCreationProcess(
            DispatchingFileSystemClient<TId> owner,
            IDestinationRevision<TId> origin,
            RootInfo<TId> root)
        {
            _owner = owner;
            _origin = origin;
            _root = root;
        }

        public NodeInfo<TId> FileInfo => _owner.AddRoot(_origin.FileInfo, _root);

        public NodeInfo<TId> BackupInfo
        {
            get => _owner.AddRoot(_origin.BackupInfo, _root);
            set => _origin.BackupInfo = value.Copy().WithRoot(null);
        }

        public bool ImmediateHydrationRequired => _origin.ImmediateHydrationRequired;
        public bool ChecksumVerificationEnabled => _origin.ChecksumVerificationEnabled;
        public bool CanGetContentStream => _origin.CanGetContentStream;

        public Stream GetContentStream()
        {
            return _origin.GetContentStream();
        }

        public Task WriteContentAsync(Stream source, FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
        {
            return _origin.WriteContentAsync(source, expectedChecksum, cancellationToken);
        }

        public async Task<NodeInfo<TId>> FinishAsync(FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
        {
            var result = await _origin.FinishAsync(expectedChecksum, cancellationToken).ConfigureAwait(false);

            return _owner.AddRoot(result, _root);
        }

        public ValueTask DisposeAsync() => _origin.DisposeAsync();
    }

    private sealed class RootedFileHydrationDemandHandler : IFileHydrationDemandHandler<TId>
    {
        private readonly IFileHydrationDemandHandler<TId> _decoratedInstance;
        private readonly RootInfo<TId> _rootInfo;

        public RootedFileHydrationDemandHandler(IFileHydrationDemandHandler<TId> decoratedInstance, RootInfo<TId> rootInfo)
        {
            _decoratedInstance = decoratedInstance;
            _rootInfo = rootInfo;
        }

        public Task HandleAsync(IFileHydrationDemand<TId> hydrationDemand, CancellationToken cancellationToken)
        {
            return _decoratedInstance.HandleAsync(new RootedFileHydrationDemand(hydrationDemand, _rootInfo), cancellationToken);
        }
    }

    private sealed class RootedFileHydrationDemand : IFileHydrationDemand<TId>
    {
        private readonly IFileHydrationDemand<TId> _origin;
        private readonly RootInfo<TId> _rootInfo;

        public RootedFileHydrationDemand(IFileHydrationDemand<TId> origin, RootInfo<TId> rootInfo)
        {
            _origin = origin;
            _rootInfo = rootInfo;
            FileInfo = _origin.FileInfo.Copy().WithRoot(rootInfo);
        }

        public NodeInfo<TId> FileInfo { get; }
        public bool ChecksumVerificationEnabled => _origin.ChecksumVerificationEnabled;
        public Stream GetHydrationStream(FileContentChecksum expectedChecksum) => _origin.GetHydrationStream(expectedChecksum);

        public NodeInfo<TId> UpdateFileSize() => _origin.UpdateFileSize().Copy().WithRoot(_rootInfo);
    }
}
