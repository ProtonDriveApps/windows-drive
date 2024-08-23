using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Adapter.UpdateDetection;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.Sync.Adapter.OperationExecution;

internal sealed class ExecutionStep<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly IFileSystemClient<TAltId> _fileSystemClient;
    private readonly IFileNameFactory<TId> _tempFileNameFactory;

    public ExecutionStep(IFileSystemClient<TAltId> fileSystemClient, IFileNameFactory<TId> tempFileNameFactory)
    {
        _fileSystemClient = fileSystemClient;
        _tempFileNameFactory = tempFileNameFactory;
    }

    public async Task<NodeInfo<TAltId>> ExecuteFileTransferAsync(
        ExecutableOperation<TId> operation,
        NodeInfo<TAltId> nodeInfo,
        IRevision sourceRevision,
        UpdateDetectionSwitch updateDetection,
        Action<Progress> progressCallback,
        CancellationToken cancellationToken)
    {
        return operation.Type switch
        {
            OperationType.Create => await CreateFileAsync(nodeInfo, operation.Model, sourceRevision, updateDetection, progressCallback, cancellationToken).ConfigureAwait(false),
            OperationType.Edit => await EditAsync(nodeInfo, operation.Model, sourceRevision, updateDetection, progressCallback, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException(),
        };
    }

    public async Task<NodeInfo<TAltId>> ExecuteSimpleOperationAsync(
        ExecutableOperation<TId> operation,
        NodeInfo<TAltId> nodeInfo,
        NodeInfo<TAltId>? destinationInfo,
        CancellationToken cancellationToken)
    {
        return operation.Type switch
        {
            OperationType.Create => await CreateFolderAsync(nodeInfo, cancellationToken).ConfigureAwait(false),
            OperationType.Move => await MoveAsync(nodeInfo, destinationInfo, cancellationToken).ConfigureAwait(false),
            OperationType.Delete => await DeleteAsync(nodeInfo, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException(),
        };
    }

    private static async Task<NodeInfo<TAltId>> FinalizeAsync(
        IRevisionCreationProcess<TAltId> creationProcess,
        UpdateDetectionSwitch updateDetection,
        CancellationToken cancellationToken)
    {
        // Finishing file transfer (creationProcess) generates remote file creation or revision
        // change events on remote file system / local temporary file rename to the
        // desired name events on local file system.
        // To make sure operation execution result is applied to the Adapter Tree before
        // processing those events, event log based update detection is postponed until
        // file transfer is finished and the result is applied to the Adapter Tree.
        await updateDetection.PostponeAsync(cancellationToken).ConfigureAwait(false);

        return await creationProcess.FinishAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<NodeInfo<TAltId>> CreateFolderAsync(
        NodeInfo<TAltId> nodeInfo,
        CancellationToken cancellationToken)
    {
        return await _fileSystemClient.CreateDirectory(nodeInfo, cancellationToken).ConfigureAwait(false);
    }

    private async Task<NodeInfo<TAltId>> CreateFileAsync(
        NodeInfo<TAltId> nodeInfo,
        FileSystemNodeModel<TId> nodeModel,
        IRevision sourceRevision,
        UpdateDetectionSwitch updateDetection,
        Action<Progress> progressCallback,
        CancellationToken cancellationToken)
    {
        nodeInfo = nodeInfo.Copy()
            .WithLastWriteTimeUtc(sourceRevision.LastWriteTimeUtc)
            .WithSize(sourceRevision.Size);

        await sourceRevision.CheckReadabilityAsync(cancellationToken).ConfigureAwait(false);

        var destinationRevision = await _fileSystemClient.CreateFile(
            nodeInfo,
            GetTempFileName(nodeModel),
            sourceRevision,
            progressCallback,
            cancellationToken).ConfigureAwait(false);

        await using (destinationRevision.ConfigureAwait(false))
        {
            return await FinishRevisionCreation(sourceRevision, destinationRevision, updateDetection, progressCallback, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<NodeInfo<TAltId>> EditAsync(
        NodeInfo<TAltId> nodeInfo,
        AltIdentifiableFileSystemNodeModel<TId, TId> nodeModel,
        IRevision sourceRevision,
        UpdateDetectionSwitch updateDetection,
        Action<Progress> progressCallback,
        CancellationToken cancellationToken)
    {
        Ensure.NotNullOrEmpty(nodeInfo.Name, nameof(nodeInfo), nameof(nodeInfo.Name));

        await sourceRevision.CheckReadabilityAsync(cancellationToken).ConfigureAwait(false);

        var destinationRevision = await _fileSystemClient.CreateRevision(
                nodeInfo,
                sourceRevision.Size,
                sourceRevision.LastWriteTimeUtc,
                GetTempFileName(nodeModel),
                sourceRevision,
                progressCallback,
                cancellationToken)
            .ConfigureAwait(false);

        await using (destinationRevision.ConfigureAwait(false))
        {
            return await FinishRevisionCreation(sourceRevision, destinationRevision, updateDetection, progressCallback, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<NodeInfo<TAltId>> FinishRevisionCreation(
        IRevision sourceRevision,
        IRevisionCreationProcess<TAltId> destinationRevision,
        UpdateDetectionSwitch updateDetection,
        Action<Progress> progressCallback,
        CancellationToken cancellationToken)
    {
        if (!destinationRevision.ImmediateHydrationRequired)
        {
            progressCallback.Invoke(new Progress(50, 100));

            return await FinalizeAsync(destinationRevision, updateDetection, cancellationToken).ConfigureAwait(false);
        }

        var destinationContent = destinationRevision.OpenContentStream();

        await using (destinationContent.ConfigureAwait(false))
        {
            var sourceContent = sourceRevision.GetContentStream();
            await using (sourceContent.ConfigureAwait(false))
            {
                await CopyFileContentAsync(destinationContent, sourceContent, cancellationToken).ConfigureAwait(false);
            }

            return await FinalizeAsync(destinationRevision, updateDetection, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<NodeInfo<TAltId>> MoveAsync(
        NodeInfo<TAltId> nodeInfo,
        NodeInfo<TAltId>? destinationInfo,
        CancellationToken cancellationToken)
    {
        Ensure.NotNull(destinationInfo, nameof(destinationInfo));

        await _fileSystemClient.Move(nodeInfo, destinationInfo, cancellationToken).ConfigureAwait(false);

        return destinationInfo;
    }

    private async Task<NodeInfo<TAltId>> DeleteAsync(
        NodeInfo<TAltId> nodeInfo,
        CancellationToken cancellationToken)
    {
        await _fileSystemClient.Delete(nodeInfo, cancellationToken).ConfigureAwait(false);

        return nodeInfo;
    }

    private async Task CopyFileContentAsync(Stream destination, Stream source, CancellationToken cancellationToken)
    {
        // The Drive encrypted file write stream requires the Length to be set before copying the content.
        // The Drive encrypted file read stream can report Length value different from the length of the unencrypted data.
        destination.SetLength(source.Length);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

        // Set the Length to the real number of bytes copied.
        if (destination.Position != destination.Length)
        {
            destination.SetLength(destination.Position);
        }

        // Destination should be flushed but not closed so that the local file remains locked.
        // It is needed to set last write time and read the file metadata before releasing the file lock.
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private string GetTempFileName(FileSystemNodeModel<TId> nodeModel)
    {
        return _tempFileNameFactory.GetName(nodeModel);
    }
}
