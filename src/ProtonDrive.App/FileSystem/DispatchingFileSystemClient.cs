using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem;

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

    public async Task<NodeInfo<TId>> GetInfo(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        var completeInfo = await client.GetInfo(info, cancellationToken).ConfigureAwait(false);

        return AddRoot(completeInfo, info.Root);
    }

    public IAsyncEnumerable<NodeInfo<TId>> Enumerate(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        if (info.IsEmpty)
        {
            return _rootToClientDictionary.Keys
                .Select(root => NodeInfo<TId>.Directory().WithId(root.NodeId).WithName(root.Id.ToString()).WithRoot(root))
                .ToAsyncEnumerable();
        }

        var client = GetClient(info);
        var result = client.Enumerate(info, cancellationToken);

        return result.Select(node => AddRoot(node, info.Root));
    }

    public async Task<NodeInfo<TId>> CreateDirectory(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        var result = await client.CreateDirectory(info, cancellationToken).ConfigureAwait(false);

        return AddRoot(result, info.Root);
    }

    public async Task<IRevisionCreationProcess<TId>> CreateFile(
        NodeInfo<TId> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        var result = await client.CreateFile(info, tempFileName, thumbnailProvider, progressCallback, cancellationToken).ConfigureAwait(false);

        return new DispatchingRevisionCreationProcess(this, result, info.Root!);
    }

    public Task<IRevision> OpenFileForReading(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        return client.OpenFileForReading(info, cancellationToken);
    }

    public async Task<IRevisionCreationProcess<TId>> CreateRevision(
        NodeInfo<TId> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        var result = await client.CreateRevision(info, size, lastWriteTime, tempFileName, thumbnailProvider, progressCallback, cancellationToken)
            .ConfigureAwait(false);

        return new DispatchingRevisionCreationProcess(this, result, info.Root!);
    }

    public Task Move(NodeInfo<TId> info, NodeInfo<TId> destinationInfo, CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        return client.Move(info, destinationInfo, cancellationToken);
    }

    public Task Delete(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        return client.Delete(info, cancellationToken);
    }

    public Task DeletePermanently(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        return client.DeletePermanently(info, cancellationToken);
    }

    public Task DeleteRevision(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        var client = GetClient(info);

        return client.DeleteRevision(info, cancellationToken);
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

    private class DispatchingRevisionCreationProcess : IRevisionCreationProcess<TId>
    {
        private readonly DispatchingFileSystemClient<TId> _owner;
        private readonly IRevisionCreationProcess<TId> _origin;
        private readonly RootInfo<TId> _root;

        public DispatchingRevisionCreationProcess(
            DispatchingFileSystemClient<TId> owner,
            IRevisionCreationProcess<TId> origin,
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
            set => _origin.BackupInfo = value.Copy().WithRoot(default);
        }

        public bool ImmediateHydrationRequired => _origin.ImmediateHydrationRequired;

        public Stream OpenContentStream()
        {
            return _origin.OpenContentStream();
        }

        public async Task<NodeInfo<TId>> FinishAsync(CancellationToken cancellationToken)
        {
            var result = await _origin.FinishAsync(cancellationToken).ConfigureAwait(false);

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

        public Stream HydrationStream => _origin.HydrationStream;

        public NodeInfo<TId> UpdateFileSize() => _origin.UpdateFileSize().Copy().WithRoot(_rootInfo);
    }
}
