using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

internal sealed class BackingUpOnDemandRevisionCreationProcess : IRevisionCreationProcess<long>
{
    private readonly FileSystemFile _file;

    public BackingUpOnDemandRevisionCreationProcess(NodeInfo<long> fileInfo, FileSystemFile file)
    {
        _file = file;
        FileInfo = fileInfo;
    }

    public NodeInfo<long> FileInfo { get; }

    public NodeInfo<long> BackupInfo { get; set; } = NodeInfo<long>.Empty();

    public bool ImmediateHydrationRequired => false;

    public IThumbnailProvider? ThumbnailProvider { get; set; }

    public Stream OpenContentStream()
    {
        throw new NotSupportedException();
    }

    public Task<NodeInfo<long>> FinishAsync(CancellationToken cancellationToken)
    {
        Ensure.IsFalse(BackupInfo.IsEmpty, $"{nameof(BackupInfo)} is required", nameof(BackupInfo));

        using var placeholderCreationInfo = FileInfo.ToPlaceholderCreationInfo();

        using var parentDirectory = FileInfo.OpenParentDirectory(FileSystemFileAccess.ReadData, FileShare.ReadWrite);

        /* Move the original file to become a backup. This doesn't ensure the whole operation is atomic. */

        var newName = BackupInfo.GetNameAndThrowIfInvalid();

        var isRename = string.IsNullOrEmpty(BackupInfo.Path);

        if (isRename)
        {
            _file.Rename(newName, includeObjectId: true);
        }
        else
        {
            using var newParent = BackupInfo.OpenParentDirectory(FileSystemFileAccess.TraverseDirectory, FileShare.ReadWrite);

            _file.Move(newParent, newName, includeObjectId: true);
        }

        /* Create a new placeholder file */

        var resultingInfo = FileInfo.CreatePlaceholderFile(parentDirectory);

        return Task.FromResult(resultingInfo);
    }

    public void Dispose()
    {
        _file.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();

        return ValueTask.CompletedTask;
    }
}
