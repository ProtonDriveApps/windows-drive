namespace ProtonDrive.Sync.Shared.FileSystem;

public interface IFileHydrationDemand<TId>
    where TId : IEquatable<TId>
{
    NodeInfo<TId> FileInfo { get; }
    bool ChecksumVerificationEnabled { get; }
    Stream GetHydrationStream(FileContentChecksum expectedChecksum);

    NodeInfo<TId> UpdateFileSize();
}
