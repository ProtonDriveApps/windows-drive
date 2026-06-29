namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

public interface IPhotoFileSystemClient<TId> : IFileSystemClient<TId>
    where TId : IEquatable<TId>
{
    IAsyncEnumerable<NodeInfo<TId>> EnumerateFoldersAsync(NodeInfo<TId> info, CancellationToken cancellationToken);

    IAsyncEnumerable<NodeInfo<TId>> EnumeratePhotoFilesAsync(NodeInfo<TId> info, CancellationToken cancellationToken);

    IAsyncEnumerable<NodeInfo<TId>> EnumerateAllPhotoFilesAsync(NodeInfo<TId> info, CancellationToken cancellationToken);
}
