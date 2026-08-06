using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem;

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

    public Task<NodeInfo<TId>> CreateDirectoryAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task<IDestinationRevision<TId>> CreateFileAsync(
        NodeInfo<TId> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public IAsyncEnumerable<NodeInfo<TId>> EnumerateAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task<NodeInfo<TId>> GetInfoAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task MoveAsync(IReadOnlyList<NodeInfo<TId>> sourceNodes, NodeInfo<TId> destinationInfo, CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task MoveAsync(NodeInfo<TId> info, NodeInfo<TId> destinationInfo, CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task<ISourceRevision> OpenFileForReadingAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task<IDestinationRevision<TId>> CreateRevisionAsync(
        NodeInfo<TId> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task DeleteAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task DeletePermanentlyAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        throw new FileSystemClientException(ExceptionMessage, FileSystemErrorCode.Offline);
    }

    public Task DeleteRevisionAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
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
