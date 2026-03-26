namespace ProtonDrive.Sync.Shared.FileSystem;

public interface IFileHydrationDemand<TId>
    where TId : IEquatable<TId>
{
    NodeInfo<TId> FileInfo { get; }
    bool ChecksumVerificationEnabled { get; }
    Stream GetHydrationStream(ReadOnlyMemory<byte>? expectedSha1);

    NodeInfo<TId> UpdateFileSize();
}
