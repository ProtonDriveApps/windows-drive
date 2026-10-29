using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Shared.Adapters;

/// <summary>
/// Provides access to file revision content
/// </summary>
/// <typeparam name="TId">Type of the node identity value</typeparam>
public interface IFileRevisionProvider<in TId>
    where TId : IEquatable<TId>
{
    /// <summary>
    /// Reads the file and provides access to the file content stream.
    /// </summary>
    /// <param name="id">File node identity value on the adapter of the replica the file resides on</param>
    /// <param name="version">File content version</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests</param>
    /// <returns>The <see cref="ISourceRevision"/></returns>
    /// <exception cref="FileRevisionProviderException"></exception>
    Task<ISourceRevision> OpenFileForReadingAsync(TId id, long version, CancellationToken cancellationToken);

    /// <summary>
    /// Validates the file against the adapter state and provides a revision with the file metadata known to the adapter.
    /// Neither the file system nor the remote API is accessed, the content of the returned revision cannot be read.
    /// If the file last write time is unknown to the adapter, the file is opened for reading instead.
    /// </summary>
    /// <param name="id">File node identity value on the adapter of the replica the file resides on</param>
    /// <param name="version">File content version</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests</param>
    /// <returns>The <see cref="ISourceRevision"/> providing only the file metadata</returns>
    /// <exception cref="FileRevisionProviderException"></exception>
    Task<ISourceRevision> GetMetadataOnlyRevisionAsync(TId id, long version, CancellationToken cancellationToken);
}
