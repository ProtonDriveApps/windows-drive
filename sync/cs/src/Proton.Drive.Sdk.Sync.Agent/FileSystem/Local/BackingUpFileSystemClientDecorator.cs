using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Local;

internal sealed class BackingUpFileSystemClientDecorator<TId> : FileSystemClientDecoratorBase<TId>
    where TId : IEquatable<TId>
{
    private readonly ILogger<BackingUpFileSystemClientDecorator<TId>> _logger;
    private readonly IFileNameFactory<TId> _backupNameFactory;

    public BackingUpFileSystemClientDecorator(
        ILogger<BackingUpFileSystemClientDecorator<TId>> logger,
        IFileNameFactory<TId> backupNameFactory,
        IFileSystemClient<TId> origin)
        : base(origin)
    {
        _logger = logger;
        _backupNameFactory = backupNameFactory;
    }

    public override async Task<IDestinationRevision<TId>> CreateRevisionAsync(
        NodeInfo<TId> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        // Archive attribute indicates the file should be backed up before overwriting
        var backup = info.Attributes.HasFlag(FileAttributes.Archive);

        var result = await base.CreateRevisionAsync(
            info,
            size,
            lastWriteTime,
            tempFileName,
            thumbnailProvider,
            fileMetadataProvider,
            progressCallback,
            cancellationToken).ConfigureAwait(false);

        return backup ? CreateRevisionWithBackup(info, result) : result;
    }

    private IDestinationRevision<TId> CreateRevisionWithBackup(NodeInfo<TId> info, IDestinationRevision<TId> revisionCreationProcess)
    {
        Ensure.NotNullOrEmpty(info.Name, nameof(info), nameof(info.Name));
        Ensure.NotNullOrEmpty(info.Path, nameof(info), nameof(info.Path));

        return new BackingUpFileWriteProcess(revisionCreationProcess, SetBackupInfo);

        void SetBackupInfo()
        {
            var backupName = _backupNameFactory.GetName(
                new FileSystemNodeModel<TId> { Type = NodeType.File, Id = info.Id!, Name = info.Name });

            revisionCreationProcess.BackupInfo = info.Copy()
                .WithParentId(info.ParentId)
                .WithPath(Path.Combine(Path.GetDirectoryName(info.Path) ?? string.Empty, backupName))
                .WithName(backupName)
                .WithAttributes(default);

            _logger.LogDebug("File \"{Path}\"/{ParentId}/{Id} will be backed up as \"{BackupName}\"", info.Path, info.ParentId, info.Id, backupName);
        }
    }

    private class BackingUpFileWriteProcess : IDestinationRevision<TId>
    {
        private readonly IDestinationRevision<TId> _decoratedInstance;
        private readonly Action _setBackupInfo;

        public BackingUpFileWriteProcess(IDestinationRevision<TId> instanceToDecorate, Action setBackupInfo)
        {
            _decoratedInstance = instanceToDecorate;
            _setBackupInfo = setBackupInfo;
        }

        public NodeInfo<TId> FileInfo => _decoratedInstance.FileInfo;

        public NodeInfo<TId> BackupInfo
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

        public Task<NodeInfo<TId>> FinishAsync(FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
        {
            _setBackupInfo.Invoke();

            return _decoratedInstance.FinishAsync(expectedChecksum, cancellationToken);
        }

        public ValueTask DisposeAsync() => _decoratedInstance.DisposeAsync();
    }
}
