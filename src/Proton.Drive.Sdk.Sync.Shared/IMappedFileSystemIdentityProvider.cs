using Proton.Drive.Sdk.Sync.Shared.Trees;

namespace Proton.Drive.Sdk.Sync.Shared;

public interface IMappedFileSystemIdentityProvider
{
    public Task<LooseCompoundAltIdentity<string>?> GetRemoteIdFromLocalIdOrDefaultAsync(
        LooseCompoundAltIdentity<long> localId,
        CancellationToken cancellationToken);

    public Task<LooseCompoundAltIdentity<string>?> GetRemoteIdFromNodeIdOrDefaultAsync(
        long nodeId,
        CancellationToken cancellationToken);
}
