using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Client.RemoteNodes;

namespace Proton.Drive.Sdk.Sync.Client.Albums;

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
