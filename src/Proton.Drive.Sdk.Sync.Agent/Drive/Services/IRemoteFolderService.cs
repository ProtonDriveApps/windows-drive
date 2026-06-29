namespace Proton.Drive.Sdk.Sync.Agent.Drive.Services;

public interface IRemoteFolderService
{
    Task<bool> FolderExistsAsync(string shareId, string linkId, CancellationToken cancellationToken);
    Task<bool> NonEmptyFolderExistsAsync(string shareId, string linkId, CancellationToken cancellationToken);
}
