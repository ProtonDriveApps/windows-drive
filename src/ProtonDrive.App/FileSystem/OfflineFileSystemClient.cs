using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem;

internal sealed class OfflineFileSystemClient<TId> : IFileSystemClient<TId>
    where TId : IEquatable<TId>
{
    private const string ExceptionMessage = "The sync root is disabled";

    public void Connect(string syncRootPath, IFileHydrationDemandHandler<TId> fileHydrationDemandHandler)
    {
        // Do nothing
    }

    public Task DisconnectAsync()
    {
        return Task.CompletedTask;
    }

    public Task<NodeInfo<TId>> CreateDirectory(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task<IRevisionCreationProcess<TId>> CreateFile(
        NodeInfo<TId> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public IAsyncEnumerable<NodeInfo<TId>> Enumerate(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task<NodeInfo<TId>> GetInfo(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task Move(NodeInfo<TId> info, NodeInfo<TId> destinationInfo, CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task<IRevision> OpenFileForReading(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task<IRevisionCreationProcess<TId>> CreateRevision(
        NodeInfo<TId> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task Delete(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task DeletePermanently(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task DeleteRevision(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public void SetInSyncState(NodeInfo<TId> info)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task HydrateFileAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }
}
