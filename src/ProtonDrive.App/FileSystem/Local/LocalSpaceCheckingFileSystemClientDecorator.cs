using System;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Local;

internal sealed class LocalSpaceCheckingFileSystemClientDecorator<TId> : FileSystemClientDecoratorBase<TId>
    where TId : IEquatable<TId>
{
    private readonly string _rootDirectoryPath;
    private readonly ILocalVolumeInfoProvider _volumeInfoProvider;
    private readonly IFileSystemClient<TId> _fileSystemClient;

    public LocalSpaceCheckingFileSystemClientDecorator(
        string rootDirectoryPath,
        ILocalVolumeInfoProvider volumeInfoProvider,
        IFileSystemClient<TId> fileSystemClient)
        : base(fileSystemClient)
    {
        _rootDirectoryPath = rootDirectoryPath;
        _volumeInfoProvider = volumeInfoProvider;
        _fileSystemClient = fileSystemClient;
    }

    public override Task<IRevisionCreationProcess<TId>> CreateFile(
        NodeInfo<TId> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        ThrowIfNotEnoughAvailableSpace(info, _rootDirectoryPath);

        return _fileSystemClient.CreateFile(info, tempFileName, thumbnailProvider, progressCallback, cancellationToken);
    }

    public override Task<IRevisionCreationProcess<TId>> CreateRevision(
        NodeInfo<TId> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        ThrowIfNotEnoughAvailableSpace(info, size, _rootDirectoryPath);

        return _fileSystemClient.CreateRevision(info, size, lastWriteTime, tempFileName, thumbnailProvider, progressCallback, cancellationToken);
    }

    private void ThrowIfNotEnoughAvailableSpace(NodeInfo<TId> info, string rootDirectoryPath)
    {
        ThrowIfNotEnoughAvailableSpace(info.Id, info.Name, info.Size, rootDirectoryPath);
    }

    private void ThrowIfNotEnoughAvailableSpace(NodeInfo<TId> info, long size, string rootDirectoryPath)
    {
        ThrowIfNotEnoughAvailableSpace(info.Id, info.Name, size, rootDirectoryPath);
    }

    private void ThrowIfNotEnoughAvailableSpace(TId? fileId, string filename, long fileSize, string rootDirectoryPath)
    {
        if (fileSize <= 0)
        {
            return;
        }

        var totalFreeSpace = _volumeInfoProvider.GetAvailableFreeSpace(rootDirectoryPath);

        if (totalFreeSpace == null || totalFreeSpace < fileSize)
        {
            throw new FileSystemClientException<TId>(
                $"Not enough free space to download the file {filename} ({fileSize} B).",
                FileSystemErrorCode.FreeSpaceExceeded,
                fileId);
        }
    }
}
