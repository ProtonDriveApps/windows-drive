using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared.IO;

namespace ProtonDrive.Sync.Shared.FileSystem;

public abstract class FileSystemClientDecoratorBase<TId> : IFileSystemClient<TId>
    where TId : IEquatable<TId>
{
    private readonly IFileSystemClient<TId> _decoratedInstance;

    protected FileSystemClientDecoratorBase(IFileSystemClient<TId> instanceToDecorate)
    {
        _decoratedInstance = instanceToDecorate;
    }

    public virtual void Connect(string syncRootPath, IFileHydrationDemandHandler<TId> fileHydrationDemandHandler)
    {
        _decoratedInstance.Connect(syncRootPath, fileHydrationDemandHandler);
    }

    public virtual Task DisconnectAsync()
    {
        return _decoratedInstance.DisconnectAsync();
    }

    public virtual Task<NodeInfo<TId>> GetInfo(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        return _decoratedInstance.GetInfo(info, cancellationToken);
    }

    public virtual IAsyncEnumerable<NodeInfo<TId>> Enumerate(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        return _decoratedInstance.Enumerate(info, cancellationToken);
    }

    public virtual Task<NodeInfo<TId>> CreateDirectory(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        return _decoratedInstance.CreateDirectory(info, cancellationToken);
    }

    public virtual Task<IRevisionCreationProcess<TId>> CreateFile(
        NodeInfo<TId> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        return _decoratedInstance.CreateFile(info, tempFileName, thumbnailProvider, progressCallback, cancellationToken);
    }

    public virtual Task<IRevision> OpenFileForReading(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        return _decoratedInstance.OpenFileForReading(info, cancellationToken);
    }

    public virtual Task<IRevisionCreationProcess<TId>> CreateRevision(
        NodeInfo<TId> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        return _decoratedInstance.CreateRevision(info, size, lastWriteTime, tempFileName, thumbnailProvider, progressCallback, cancellationToken);
    }

    public virtual Task Move(NodeInfo<TId> info, NodeInfo<TId> destinationInfo, CancellationToken cancellationToken)
    {
        return _decoratedInstance.Move(info, destinationInfo, cancellationToken);
    }

    public virtual Task Delete(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        return _decoratedInstance.Delete(info, cancellationToken);
    }

    public virtual Task DeletePermanently(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        return _decoratedInstance.DeletePermanently(info, cancellationToken);
    }

    public virtual Task DeleteRevision(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        return _decoratedInstance.DeleteRevision(info, cancellationToken);
    }

    public virtual void SetInSyncState(NodeInfo<TId> info)
    {
        _decoratedInstance.SetInSyncState(info);
    }

    public virtual Task HydrateFileAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        return _decoratedInstance.HydrateFileAsync(info, cancellationToken);
    }
}
