using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

internal sealed class OnDemandFileCreationProcess : IDestinationRevision<long>
{
    private readonly FileSystemDirectory _parentDirectory;

    public OnDemandFileCreationProcess(NodeInfo<long> fileInfo, FileSystemDirectory parentDirectory)
    {
        _parentDirectory = parentDirectory;
        FileInfo = fileInfo;
    }

    public NodeInfo<long> FileInfo { get; }

    public NodeInfo<long> BackupInfo { get; set; } = NodeInfo<long>.Empty();

    public bool ImmediateHydrationRequired => false;
    public bool ChecksumVerificationEnabled => false;
    public bool CanGetContentStream => false;

    public Stream GetContentStream()
    {
        throw new NotSupportedException();
    }

    public Task WriteContentAsync(Stream source, FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<NodeInfo<long>> FinishAsync(FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
    {
        return Task.FromResult(FileInfo.CreatePlaceholderFile(_parentDirectory));
    }

    public void Dispose()
    {
        _parentDirectory.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();

        return ValueTask.CompletedTask;
    }
}
