using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.App.FileSystem.Local;

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

    public override async Task<IRevisionCreationProcess<TId>> CreateRevision(
        NodeInfo<TId> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        // Archive attribute indicates the file should be backed up before overwriting
        var backup = info.Attributes.HasFlag(FileAttributes.Archive);

        var result = await base.CreateRevision(info, size, lastWriteTime, tempFileName, thumbnailProvider, progressCallback, cancellationToken).ConfigureAwait(false);

        return backup ? CreateRevisionWithBackup(info, result) : result;
    }

    private IRevisionCreationProcess<TId> CreateRevisionWithBackup(NodeInfo<TId> info, IRevisionCreationProcess<TId> revisionCreationProcess)
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

    private class BackingUpFileWriteProcess : IRevisionCreationProcess<TId>
    {
        private readonly IRevisionCreationProcess<TId> _decoratedInstance;
        private readonly Action _setBackupInfo;

        public BackingUpFileWriteProcess(IRevisionCreationProcess<TId> instanceToDecorate, Action setBackupInfo)
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

        public Stream OpenContentStream() => _decoratedInstance.OpenContentStream();

        public Task<NodeInfo<TId>> FinishAsync(CancellationToken cancellationToken)
        {
            _setBackupInfo.Invoke();

            return _decoratedInstance.FinishAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() => _decoratedInstance.DisposeAsync();
    }
}
