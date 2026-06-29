namespace Proton.Drive.Sdk.Sync.Shared;

public interface IRemoteIdsFromLocalPathProvider
{
    Task<RemoteIds?> GetRemoteIdsOrDefaultAsync(string localPath, CancellationToken cancellationToken);
}
