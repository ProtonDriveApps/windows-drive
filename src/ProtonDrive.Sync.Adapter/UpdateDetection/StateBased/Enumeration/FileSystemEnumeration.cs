using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Adapter.UpdateDetection.StateBased.Enumeration;

internal class FileSystemEnumeration<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly IFileSystemClient<TAltId> _fileSystemClient;
    private readonly IReadOnlyDictionary<TId, RootInfo<TAltId>> _syncRoots;

    public FileSystemEnumeration(
        IFileSystemClient<TAltId> fileSystemClient,
        IReadOnlyDictionary<TId, RootInfo<TAltId>> syncRoots)
    {
        _fileSystemClient = fileSystemClient;
        _syncRoots = syncRoots;
    }

    public Task<NodeInfo<TAltId>> EnumerateNode(NodeInfo<TAltId> nodeInfo, CancellationToken cancellationToken)
    {
        return _fileSystemClient.GetInfo(nodeInfo, cancellationToken);
    }

    public IAsyncEnumerable<NodeInfo<TAltId>> EnumerateChildren(NodeInfo<TAltId> nodeInfo, CancellationToken cancellationToken)
    {
        return _fileSystemClient.Enumerate(nodeInfo, cancellationToken);
    }

    public NodeInfo<TAltId> ToNodeInfo(AdapterTreeNode<TId, TAltId> node)
    {
        return node.ToNodeInfo(_syncRoots);
    }
}
