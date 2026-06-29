using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Client.Health;

public interface IRemoteFileMetadataProvider
{
    Task<IReadOnlyList<NodeInfo<string>>> GetFileMetadataAsync(
        IReadOnlyList<string> remoteIds,
        string shareId,
        CancellationToken cancellationToken);
}
