using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Shared.Adapters;

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
}
