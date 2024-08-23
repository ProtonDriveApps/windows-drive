using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Sync.Shared.Adapters;

/// <summary>
/// Provides an identity value of the node in the File System Adapter of other replica
/// based on the node mapping handled by the Sync Engine.
/// </summary>
/// <typeparam name="TId">Type of identity value.</typeparam>
public interface IMappedNodeIdentityProvider<TId>
    where TId : struct, IEquatable<TId>
{
    /// <summary>
    /// Returns an identity value of the node in the File System Adapter of other replica.
    /// </summary>
    /// <param name="id">Node identity value on the adapter.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Mapped node identity value on other adapter. Default value if there is no mapped node.</returns>
    /// <remarks>
    /// Adapter Tree nodes in local and remote File System Adapters are considered mapped when they represent the
    /// local/remote pair of files or folders. But the identities of mapped nodes do not necessarily
    /// are the same. Mapping between local and remote Adapter Tree nodes is handled by the Sync Engine.
    /// </remarks>
    Task<TId?> GetMappedNodeIdOrDefaultAsync(TId id, CancellationToken cancellationToken);
}
