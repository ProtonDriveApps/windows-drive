using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

internal sealed class OnDemandFileCreationProcess : IRevisionCreationProcess<long>
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

    public Stream OpenContentStream()
    {
        throw new NotSupportedException();
    }

    public Task<NodeInfo<long>> FinishAsync(CancellationToken cancellationToken)
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
