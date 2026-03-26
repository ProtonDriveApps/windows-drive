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
        ISourceRevision sourceRevision,
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
        ReadOnlyMemory<byte>? expectedSha1,
        IDestinationRevision<TAltId> destinationRevision,
        UpdateDetectionSwitch updateDetection,
        CancellationToken cancellationToken)
    {
        // Finishing file transfer (destinationRevision) generates remote file creation or revision
        // change events on remote file system / local temporary file rename to the
        // desired name events on local file system.
        // To make sure operation execution result is applied to the Adapter Tree before
        // processing those events, event log based update detection is postponed until
        // file transfer is finished and the result is applied to the Adapter Tree.
        await updateDetection.PostponeAsync(cancellationToken).ConfigureAwait(false);

        return await destinationRevision.FinishAsync(expectedSha1, cancellationToken).ConfigureAwait(false);
    }

    private async Task<NodeInfo<TAltId>> CreateFolderAsync(
        NodeInfo<TAltId> nodeInfo,
        CancellationToken cancellationToken)
    {
        return await _fileSystemClient.CreateDirectoryAsync(nodeInfo, cancellationToken).ConfigureAwait(false);
    }

    private async Task<NodeInfo<TAltId>> CreateFileAsync(
        NodeInfo<TAltId> nodeInfo,
        FileSystemNodeModel<TId> nodeModel,
        ISourceRevision sourceRevision,
        UpdateDetectionSwitch updateDetection,
        Action<Progress> progressCallback,
        CancellationToken cancellationToken)
    {
        nodeInfo = nodeInfo.Copy()
            .WithLastWriteTimeUtc(sourceRevision.LastWriteTimeUtc)
            .WithSize(sourceRevision.Size);

        var destinationRevision = await _fileSystemClient.CreateFileAsync(
            nodeInfo,
            GetTempFileName(nodeModel),
            sourceRevision,
            sourceRevision, // This looks awkward, but it's intentional: IRevision implements both IThumbnailProvider and IFileMetadataProvider
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
        ISourceRevision sourceRevision,
        UpdateDetectionSwitch updateDetection,
        Action<Progress> progressCallback,
        CancellationToken cancellationToken)
    {
        Ensure.NotNullOrEmpty(nodeInfo.Name, nameof(nodeInfo), nameof(nodeInfo.Name));

        var destinationRevision = await _fileSystemClient.CreateRevisionAsync(
                nodeInfo,
                sourceRevision.Size,
                sourceRevision.LastWriteTimeUtc,
                GetTempFileName(nodeModel),
                sourceRevision,
                sourceRevision, // This looks awkward, but it's intentional: IRevision implements both IThumbnailProvider and IFileMetadataProvider
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
        ISourceRevision sourceRevision,
        IDestinationRevision<TAltId> destinationRevision,
        UpdateDetectionSwitch updateDetection,
        Action<Progress> progressCallback,
        CancellationToken cancellationToken)
    {
        if (!destinationRevision.ImmediateHydrationRequired)
        {
            return await FinalizeAsync(expectedSha1: null, destinationRevision, updateDetection, cancellationToken).ConfigureAwait(false);
        }

        // Invoking progress callback changes sync activity stage from Preparation into Execution
        progressCallback.Invoke(Progress.Zero);

        var sha1 = destinationRevision.ChecksumVerificationEnabled
            ? await sourceRevision.GetSha1Async(cancellationToken).ConfigureAwait(false)
            : null;

        await sourceRevision.CopyContentToAsync(destinationRevision, sha1, cancellationToken).ConfigureAwait(false);

        return await FinalizeAsync(sha1, destinationRevision, updateDetection, cancellationToken).ConfigureAwait(false);
    }

    private async Task<NodeInfo<TAltId>> MoveAsync(
        NodeInfo<TAltId> nodeInfo,
        NodeInfo<TAltId>? destinationInfo,
        CancellationToken cancellationToken)
    {
        Ensure.NotNull(destinationInfo, nameof(destinationInfo));

        await _fileSystemClient.MoveAsync(nodeInfo, destinationInfo, cancellationToken).ConfigureAwait(false);

        return destinationInfo;
    }

    private async Task<NodeInfo<TAltId>> DeleteAsync(
        NodeInfo<TAltId> nodeInfo,
        CancellationToken cancellationToken)
    {
        await _fileSystemClient.DeleteAsync(nodeInfo, cancellationToken).ConfigureAwait(false);

        return nodeInfo;
    }

    private string GetTempFileName(FileSystemNodeModel<TId> nodeModel)
    {
        return _tempFileNameFactory.GetName(nodeModel);
    }
}
