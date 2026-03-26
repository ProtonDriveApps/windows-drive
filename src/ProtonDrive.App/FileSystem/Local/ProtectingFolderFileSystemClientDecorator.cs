using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Shared;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Local;

internal sealed class ProtectingFolderFileSystemClientDecorator : FileSystemClientDecoratorBase<long>
{
    private readonly ISyncFolderStructureProtector _folderStructureProtector;
    private readonly ConcurrentFolderStructureProtector<long> _concurrentFolderStructureProtector;

    public ProtectingFolderFileSystemClientDecorator(ISyncFolderStructureProtector folderStructureProtector, IFileSystemClient<long> decoratedInstance)
        : base(decoratedInstance)
    {
        _folderStructureProtector = folderStructureProtector;

        _concurrentFolderStructureProtector = new ConcurrentFolderStructureProtector<long>(_folderStructureProtector);
    }

    public override async Task<NodeInfo<long>> CreateDirectoryAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        await using ((await UnprotectParentFolderAsync(info, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
        {
            var resultInfo = await base.CreateDirectoryAsync(info, cancellationToken).ConfigureAwait(false);

            // CreateDirectory does not fill the Path, therefore we cannot use resultInfo for protecting folder
            ProtectFolder(info);

            return resultInfo;
        }
    }

    public override async Task<IDestinationRevision<long>> CreateFileAsync(
        NodeInfo<long> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        var parentFolderProtectionHolder = await UnprotectParentFolderAsync(info, cancellationToken).ConfigureAwait(false);

        try
        {
            var revisionCreationProcess = await base.CreateFileAsync(
                info,
                tempFileName,
                thumbnailProvider,
                fileMetadataProvider,
                progressCallback,
                cancellationToken).ConfigureAwait(false);

            return new ProtectingRevisionCreationProcess(this, revisionCreationProcess, parentFolderProtectionHolder);
        }
        catch
        {
            await parentFolderProtectionHolder.DisposeAsync().ConfigureAwait(false);

            throw;
        }
    }

    public override async Task<IDestinationRevision<long>> CreateRevisionAsync(
        NodeInfo<long> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        var parentFolderProtectionHolder = await UnprotectParentFolderAsync(info, cancellationToken).ConfigureAwait(false);

        try
        {
            UnprotectFile(info);

            try
            {
                var revisionCreationProcess = await base.CreateRevisionAsync(
                    info,
                    size,
                    lastWriteTime,
                    tempFileName,
                    thumbnailProvider,
                    fileMetadataProvider,
                    progressCallback,
                    cancellationToken).ConfigureAwait(false);

                return new ProtectingRevisionCreationProcess(this, revisionCreationProcess, parentFolderProtectionHolder);
            }
            catch
            {
                ProtectFile(info);
                throw;
            }
        }
        catch
        {
            await parentFolderProtectionHolder.DisposeAsync().ConfigureAwait(false);

            throw;
        }
    }

    public override async Task MoveAsync(NodeInfo<long> info, NodeInfo<long> newInfo, CancellationToken cancellationToken)
    {
        await using ((await UnprotectParentFolderAsync(info, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
        await using ((await UnprotectParentFolderAsync(newInfo, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
        {
            await base.MoveAsync(info, newInfo, cancellationToken).ConfigureAwait(false);
        }
    }

    public override async Task DeleteAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        await using ((await UnprotectParentFolderAsync(info, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
        {
            UnprotectFileOrBranch(info);

            try
            {
                await base.DeleteAsync(info, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // In case of failure, we do not add branch protection
                ProtectFileOrFolder(info);

                throw;
            }
        }
    }

    public override async Task DeletePermanentlyAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        await using ((await UnprotectParentFolderAsync(info, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
        {
            UnprotectFileOrBranch(info);

            try
            {
                await base.DeletePermanentlyAsync(info, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // In case of failure, we do not add branch protection
                ProtectFileOrFolder(info);

                throw;
            }
        }
    }

    private Task<IAsyncDisposable> UnprotectParentFolderAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        var parentPath = info.GetParentFolderPath();

        if (string.IsNullOrEmpty(parentPath) || info.ParentId == 0)
        {
            return Task.FromResult(AsyncDisposable.Empty);
        }

        return _concurrentFolderStructureProtector.UnprotectFolderAsync(info.ParentId, parentPath, cancellationToken);
    }

    private void ProtectFileOrFolder(NodeInfo<long> info)
    {
        if (info.IsFile())
        {
            ProtectFile(info);
        }
        else
        {
            ProtectFolder(info);
        }
    }

    private void UnprotectFileOrBranch(NodeInfo<long> info)
    {
        if (info.IsFile())
        {
            UnprotectFile(info);
        }
        else
        {
            UnprotectBranch(info);
        }
    }

    private void ProtectFolder(NodeInfo<long> info)
    {
        Ensure.IsTrue(info.IsDirectory(), "Must be a folder", nameof(info));

        _folderStructureProtector.ProtectFolder(info.Path, FolderProtectionType.ReadOnly);
    }

    private void UnprotectBranch(NodeInfo<long> info)
    {
        Ensure.IsTrue(info.IsDirectory(), "Must be a folder", nameof(info));

        _folderStructureProtector.UnprotectBranch(info.Path, FolderProtectionType.ReadOnly, FileProtectionType.ReadOnly);
    }

    private void ProtectFile(NodeInfo<long> info)
    {
        Ensure.IsTrue(info.IsFile(), "Must be a file", nameof(info));

        _folderStructureProtector.ProtectFile(info.Path, FileProtectionType.ReadOnly);
    }

    private void UnprotectFile(NodeInfo<long> info)
    {
        Ensure.IsTrue(info.IsFile(), "Must be a file", nameof(info));

        _folderStructureProtector.UnprotectFile(info.Path, FileProtectionType.ReadOnly);
    }

    private sealed class ProtectingRevisionCreationProcess : IDestinationRevision<long>
    {
        private readonly ProtectingFolderFileSystemClientDecorator _fileProtector;
        private readonly IDestinationRevision<long> _decoratedInstance;
        private readonly IAsyncDisposable _parentFolderProtectionHolder;

        public ProtectingRevisionCreationProcess(
            ProtectingFolderFileSystemClientDecorator fileProtector,
            IDestinationRevision<long> decoratedInstance,
            IAsyncDisposable parentFolderProtectionHolder)
        {
            _fileProtector = fileProtector;
            _decoratedInstance = decoratedInstance;
            _parentFolderProtectionHolder = parentFolderProtectionHolder;
        }

        public NodeInfo<long> FileInfo => _decoratedInstance.FileInfo;

        public NodeInfo<long> BackupInfo
        {
            get => _decoratedInstance.BackupInfo;
            set => _decoratedInstance.BackupInfo = value;
        }

        public bool ImmediateHydrationRequired => _decoratedInstance.ImmediateHydrationRequired;
        public bool ChecksumVerificationEnabled => _decoratedInstance.ChecksumVerificationEnabled;
        public bool CanGetContentStream => _decoratedInstance.CanGetContentStream;

        public Stream GetContentStream()
        {
            return _decoratedInstance.GetContentStream();
        }

        public Task WriteContentAsync(Stream source, ReadOnlyMemory<byte>? expectedSha1, CancellationToken cancellationToken)
        {
            return _decoratedInstance.WriteContentAsync(source, expectedSha1, cancellationToken);
        }

        public Task<NodeInfo<long>> FinishAsync(ReadOnlyMemory<byte>? expectedSha1, CancellationToken cancellationToken)
        {
            return _decoratedInstance.FinishAsync(expectedSha1, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _parentFolderProtectionHolder.DisposeAsync().ConfigureAwait(false);
            _fileProtector.ProtectFile(FileInfo);

            await _decoratedInstance.DisposeAsync().ConfigureAwait(false);
        }
    }
}
