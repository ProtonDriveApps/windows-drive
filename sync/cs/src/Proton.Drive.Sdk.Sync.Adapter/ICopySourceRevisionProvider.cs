using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Adapter;

internal interface ICopySourceRevisionProvider<TId, TAltId>
    where TId : struct, IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    /// <summary>
    /// Opens for reading the file the specified node has been copied from, when a move between
    /// move scopes has been replaced with copying and deletion of the source node.
    /// </summary>
    /// <param name="id">The identity of the Adapter Tree node that is the destination of copying.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The source revision, or <see langword="null"/> if the node is not a destination
    /// of copying or the copy source is not mapped.</returns>
    Task<ISourceRevision?> OpenForReadingAsync(TId id, CancellationToken cancellationToken);
}
