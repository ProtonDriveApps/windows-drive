using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client.Albums;

public interface IAlbumNodeService
{
    Task<string> GetAlbumNameAsync(string shareId, Link link, CancellationToken cancellationToken);
}
