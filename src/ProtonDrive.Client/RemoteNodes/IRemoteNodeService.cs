using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client.RemoteNodes;

internal interface IRemoteNodeService
{
    Task<RemoteNode> GetRemoteNodeAsync(string shareId, string linkId, CancellationToken cancellationToken);
    Task<RemoteNode> GetRemoteNodeAsync(string shareId, Link link, CancellationToken cancellationToken);
    Task<RemoteNode> GetRemoteNodeAsync(IPrivateKeyHolder parent, Link link, CancellationToken cancellationToken);

    /// <summary>
    /// From an ordered list of hierarchy links, it returns the last node and facilitate the caching of ancestor nodes.
    /// </summary>
    /// <param name="rootShareId"></param>
    /// <param name="linksHierarchy">Ordered list (from the root to the node) containing all the ancestor nodes of the target node.
    /// This hierarchical list facilitates the caching process.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Gets the node details from its hierarchy.</returns>
    Task<RemoteNode> GetRemoteNodeFromHierarchyAsync(string rootShareId, IImmutableList<Link> linksHierarchy, CancellationToken cancellationToken);

    Task<Share> GetShareAsync(string shareId, CancellationToken cancellationToken);
}
