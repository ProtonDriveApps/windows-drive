namespace ProtonDrive.Sync.Shared.FileSystem;

public interface IDestinationRevision<TId> : IAsyncDisposable
    where TId : IEquatable<TId>
{
    NodeInfo<TId> FileInfo { get; }
    NodeInfo<TId> BackupInfo { get; set; }
    bool ImmediateHydrationRequired { get; }
    bool ChecksumVerificationEnabled { get; }
    bool CanGetContentStream { get; }

    Stream GetContentStream();
    Task WriteContentAsync(Stream source, FileContentChecksum expectedChecksum, CancellationToken cancellationToken);
    Task<NodeInfo<TId>> FinishAsync(FileContentChecksum expectedChecksum, CancellationToken cancellationToken);
}
