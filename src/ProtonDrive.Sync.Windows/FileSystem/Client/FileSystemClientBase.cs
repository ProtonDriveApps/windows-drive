using ProtonDrive.Shared;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

internal abstract class FileSystemClientBase
{
    public Task DeleteAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // We cancel awaiting to move to the Recycle Bin, but the request continues
        return DeleteAsync(info, (fsObject, ct) => RecycleBin.MoveToRecycleBinAsync(fsObject.FullPath).WaitAsync(ct), cancellationToken);
    }

    public Task DeletePermanentlyAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return DeleteAsync(info, (fsObject, _) => DeleteFileOrFolder(fsObject), cancellationToken);

        Task DeleteFileOrFolder(FileSystemObject fsObject)
        {
            fsObject.Delete(info);

            return Task.CompletedTask;
        }
    }

    private static async Task DeleteAsync(
        NodeInfo<long> info,
        Func<FileSystemObject, CancellationToken, Task> deletionFunction,
        CancellationToken cancellationToken)
    {
        Ensure.NotNullOrEmpty(info.Path, nameof(info), nameof(info.Path));

        var access = info.Attributes.GetAccessForDeletion(deleteReadOnly: true);

        using var fsObject = info.Open(access, FileShare.Read | FileShare.Delete);

        fsObject.ThrowIfMetadataMismatch(info);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await deletionFunction.Invoke(fsObject, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, info.Id, out var mappedException))
        {
            throw mappedException;
        }
    }
}
