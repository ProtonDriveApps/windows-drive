using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.RemoteNodes;

namespace ProtonDrive.Client.Albums;

internal sealed class AlbumNodeService : IAlbumNodeService
{
    private readonly IRemoteNodeService _remoteNodeService;

    public AlbumNodeService(IRemoteNodeService remoteNodeService)
    {
        _remoteNodeService = remoteNodeService;
    }

    public async Task<string> GetAlbumNameAsync(string shareId, Link link, CancellationToken cancellationToken)
    {
        var node = await _remoteNodeService.GetRemoteNodeAsync(shareId, link, cancellationToken).ConfigureAwait(false);

        return node.Name;
    }
}
