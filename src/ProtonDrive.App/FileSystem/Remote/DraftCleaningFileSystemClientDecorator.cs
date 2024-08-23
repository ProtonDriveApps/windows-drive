using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Adapter;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Remote;

internal sealed class DraftCleaningFileSystemClientDecorator : FileSystemClientDecoratorBase<string>
{
    private readonly IRevisionUploadAttemptRepository _revisionUploadAttemptRepository;

    public DraftCleaningFileSystemClientDecorator(
        IRevisionUploadAttemptRepository revisionUploadAttemptRepository,
        IFileSystemClient<string> instanceToDecorate)
        : base(instanceToDecorate)
    {
        _revisionUploadAttemptRepository = revisionUploadAttemptRepository;
    }

    public async override Task<IRevisionCreationProcess<string>> CreateFile(
        NodeInfo<string> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        await DeletePreviousDraftIfExists(info, isForCreation: true, cancellationToken).ConfigureAwait(false);

        var fileHydrationProcess = await base.CreateFile(info, tempFileName, thumbnailProvider, progressCallback, cancellationToken).ConfigureAwait(false);

        var createdFileInfo = fileHydrationProcess.FileInfo;

        await _revisionUploadAttemptRepository.AddAsync(
            createdFileInfo.ParentId!,
            createdFileInfo.Name,
            createdFileInfo.Id!,
            null).ConfigureAwait(false);

        return new RevisionCreationProcessDecorator(fileHydrationProcess, DeleteFromRepositoryAsync);

        Task DeleteFromRepositoryAsync() => _revisionUploadAttemptRepository.DeleteAsync(info.ParentId!, info.Name);
    }

    public async override Task<IRevisionCreationProcess<string>> CreateRevision(
        NodeInfo<string> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        await DeletePreviousDraftIfExists(info, isForCreation: false, cancellationToken).ConfigureAwait(false);

        var fileHydrationProcess = await base.CreateRevision(info, size, lastWriteTime, tempFileName, thumbnailProvider, progressCallback, cancellationToken)
            .ConfigureAwait(false);

        var existingFileInfo = fileHydrationProcess.FileInfo;

        await _revisionUploadAttemptRepository.AddAsync(
            existingFileInfo.ParentId!,
            existingFileInfo.Name,
            existingFileInfo.Id!,
            existingFileInfo.RevisionId).ConfigureAwait(false);

        return new RevisionCreationProcessDecorator(fileHydrationProcess, DeleteFromRepositoryAsync);

        Task DeleteFromRepositoryAsync() => _revisionUploadAttemptRepository.DeleteAsync(info.ParentId!, info.Name);
    }

    private async Task DeletePreviousDraftIfExists(NodeInfo<string> info, bool isForCreation, CancellationToken cancellationToken)
    {
        var previousAttempt = await _revisionUploadAttemptRepository.GetAsync(info.ParentId!, info.Name).ConfigureAwait(false);
        if (previousAttempt is null)
        {
            return;
        }

        var (fileId, revisionId) = previousAttempt.Value;

        // We only delete the draft if the nature (file / revision) of the one that was recorded is the same as what we're creating.
        // Otherwise, we let the decorated instance deal with the conflict.
        if (RecordedDraftShouldBeAndIsFile() || RecordedDraftShouldBeAndIsRevision())
        {
            var infoForDeletion = NodeInfo<string>.File().WithId(fileId).WithRevisionId(revisionId).WithName(info.Name);

            try
            {
                var remoteInfo = await GetInfo(infoForDeletion, cancellationToken).ConfigureAwait(false);
                var remoteFileIsDraft = remoteInfo.RevisionId is null;

                if ((isForCreation && remoteFileIsDraft) || (!isForCreation && !remoteFileIsDraft && remoteInfo.RevisionId != revisionId))
                {
                    if (remoteFileIsDraft)
                    {
                        await DeletePermanently(infoForDeletion, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await DeleteRevision(infoForDeletion, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (FileSystemClientException ex) when (ex.ErrorCode == FileSystemErrorCode.ObjectNotFound)
            {
                // That could happen if the draft was garbage collected, or deleted some other way by another client.
                // We don't need to complain about something being absent when its absence is exactly what we wanted to accomplish by deleting it.
                // So we do nothing.
            }
        }

        await _revisionUploadAttemptRepository.DeleteAsync(info.ParentId!, info.Name).ConfigureAwait(false);

        return;

        bool RecordedDraftShouldBeAndIsFile() => isForCreation && revisionId is null;

        bool RecordedDraftShouldBeAndIsRevision() => !isForCreation && revisionId is not null;
    }

    private sealed class RevisionCreationProcessDecorator : IRevisionCreationProcess<string>
    {
        private readonly IRevisionCreationProcess<string> _decoratedInstance;
        private readonly Func<Task> _deleteFromRepositoryAction;

        public RevisionCreationProcessDecorator(
            IRevisionCreationProcess<string> instanceToDecorate,
            Func<Task> deleteFromRepositoryAction)
        {
            _decoratedInstance = instanceToDecorate;
            _deleteFromRepositoryAction = deleteFromRepositoryAction;
        }

        public NodeInfo<string> FileInfo => _decoratedInstance.FileInfo;

        public NodeInfo<string> BackupInfo
        {
            get => _decoratedInstance.BackupInfo;
            set => _decoratedInstance.BackupInfo = value;
        }

        public bool ImmediateHydrationRequired => _decoratedInstance.ImmediateHydrationRequired;

        public Stream OpenContentStream()
        {
            return _decoratedInstance.OpenContentStream();
        }

        public async Task<NodeInfo<string>> FinishAsync(CancellationToken cancellationToken)
        {
            var result = await _decoratedInstance.FinishAsync(cancellationToken).ConfigureAwait(false);

            await _deleteFromRepositoryAction.Invoke().ConfigureAwait(false);

            return result;
        }

        public ValueTask DisposeAsync()
        {
            return _decoratedInstance.DisposeAsync();
        }
    }
}
