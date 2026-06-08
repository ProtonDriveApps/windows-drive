using ProtonDrive.Shared;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.FileSystem.Integration;

namespace ProtonDrive.App.FileSystem.Local;

internal sealed class ProtectingFileFileSystemClientDecorator : FileSystemClientDecoratorBase<long>
{
    private readonly ISyncFolderStructureProtector _folderStructureProtector;

    public ProtectingFileFileSystemClientDecorator(ISyncFolderStructureProtector folderStructureProtector, IFileSystemClient<long> decoratedInstance)
        : base(decoratedInstance)
    {
        _folderStructureProtector = folderStructureProtector;
    }

    public override async Task<IDestinationRevision<long>> CreateFileAsync(
        NodeInfo<long> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        var revisionCreationProcess = await base.CreateFileAsync(info, tempFileName, thumbnailProvider, fileMetadataProvider, progressCallback, cancellationToken)
            .ConfigureAwait(false);

        return new ProtectingRevisionCreationProcess(this, revisionCreationProcess);
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

            return new ProtectingRevisionCreationProcess(this, revisionCreationProcess);
        }
        catch
        {
            ProtectFile(info);
            throw;
        }
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
        private readonly ProtectingFileFileSystemClientDecorator _fileProtector;
        private readonly IDestinationRevision<long> _decoratedInstance;

        public ProtectingRevisionCreationProcess(ProtectingFileFileSystemClientDecorator fileProtector, IDestinationRevision<long> decoratedInstance)
        {
            _fileProtector = fileProtector;
            _decoratedInstance = decoratedInstance;
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

        public Task WriteContentAsync(Stream source, FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
        {
            return _decoratedInstance.WriteContentAsync(source, expectedChecksum, cancellationToken);
        }

        public Task<NodeInfo<long>> FinishAsync(FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
        {
            return _decoratedInstance.FinishAsync(expectedChecksum, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            _fileProtector.ProtectFile(FileInfo);

            return _decoratedInstance.DisposeAsync();
        }
    }
}
