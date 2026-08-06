using Proton.Drive.Sdk.Sync.Client.Contracts;

namespace Proton.Drive.Sdk.Sync.Client.Albums;

public interface IAlbumNodeService
{
    Task<string> GetAlbumNameAsync(string shareId, Link link, CancellationToken cancellationToken);
}
