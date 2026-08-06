using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

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

    public virtual Task<NodeInfo<TId>> GetInfoAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        return _decoratedInstance.GetInfoAsync(info, cancellationToken);
    }

    public virtual IAsyncEnumerable<NodeInfo<TId>> EnumerateAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        return _decoratedInstance.EnumerateAsync(info, cancellationToken);
    }

    public virtual Task<NodeInfo<TId>> CreateDirectoryAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        return _decoratedInstance.CreateDirectoryAsync(info, cancellationToken);
    }

    public virtual Task<IDestinationRevision<TId>> CreateFileAsync(
        NodeInfo<TId> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        return _decoratedInstance.CreateFileAsync(info, tempFileName, thumbnailProvider, fileMetadataProvider, progressCallback, cancellationToken);
    }

    public virtual Task<ISourceRevision> OpenFileForReadingAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        return _decoratedInstance.OpenFileForReadingAsync(info, cancellationToken);
    }

    public virtual Task<IDestinationRevision<TId>> CreateRevisionAsync(
        NodeInfo<TId> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        return _decoratedInstance.CreateRevisionAsync(
            info,
            size,
            lastWriteTime,
            tempFileName,
            thumbnailProvider,
            fileMetadataProvider,
            progressCallback,
            cancellationToken);
    }

    public virtual Task MoveAsync(IReadOnlyList<NodeInfo<TId>> sourceNodes, NodeInfo<TId> destinationInfo, CancellationToken cancellationToken)
    {
        return _decoratedInstance.MoveAsync(sourceNodes, destinationInfo, cancellationToken);
    }

    public virtual Task MoveAsync(NodeInfo<TId> info, NodeInfo<TId> destinationInfo, CancellationToken cancellationToken)
    {
        return _decoratedInstance.MoveAsync(info, destinationInfo, cancellationToken);
    }

    public virtual Task DeleteAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        return _decoratedInstance.DeleteAsync(info, cancellationToken);
    }

    public virtual Task DeletePermanentlyAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        return _decoratedInstance.DeletePermanentlyAsync(info, cancellationToken);
    }

    public virtual Task DeleteRevisionAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        return _decoratedInstance.DeleteRevisionAsync(info, cancellationToken);
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
