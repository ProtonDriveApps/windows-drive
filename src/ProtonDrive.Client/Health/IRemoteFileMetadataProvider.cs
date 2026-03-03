using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client.Health;

public interface IRemoteFileMetadataProvider
{
    Task<IReadOnlyList<NodeInfo<string>>> GetFileMetadataAsync(
        IReadOnlyList<string> remoteIds,
        string shareId,
        CancellationToken cancellationToken);
}
