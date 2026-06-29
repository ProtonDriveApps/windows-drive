namespace Proton.Drive.Sdk.Sync.Shared;

public interface IRemoteIdsFromNodeIdProvider
{
    Task<RemoteIds?> GetRemoteIdsOrDefaultAsync(int mappingId, long remoteNodeId, CancellationToken cancellationToken);
}
