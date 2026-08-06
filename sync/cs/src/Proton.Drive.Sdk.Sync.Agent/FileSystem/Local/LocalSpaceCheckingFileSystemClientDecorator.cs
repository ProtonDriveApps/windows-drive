using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Local;

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

    public override Task<IDestinationRevision<TId>> CreateFileAsync(
        NodeInfo<TId> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        ThrowIfInsufficientLocalFreeSpace(info, _rootDirectoryPath);

        return _fileSystemClient.CreateFileAsync(info, tempFileName, thumbnailProvider, fileMetadataProvider, progressCallback, cancellationToken);
    }

    public override Task<IDestinationRevision<TId>> CreateRevisionAsync(
        NodeInfo<TId> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        ThrowIfInsufficientLocalFreeSpace(info, size, _rootDirectoryPath);

        return _fileSystemClient.CreateRevisionAsync(
            info,
            size,
            lastWriteTime,
            tempFileName,
            thumbnailProvider,
            fileMetadataProvider,
            progressCallback,
            cancellationToken);
    }

    private void ThrowIfInsufficientLocalFreeSpace(NodeInfo<TId> info, string rootDirectoryPath)
    {
        ThrowIfInsufficientLocalFreeSpace(info.Id, info.Name, info.Size, rootDirectoryPath);
    }

    private void ThrowIfInsufficientLocalFreeSpace(NodeInfo<TId> info, long size, string rootDirectoryPath)
    {
        ThrowIfInsufficientLocalFreeSpace(info.Id, info.Name, size, rootDirectoryPath);
    }

    private void ThrowIfInsufficientLocalFreeSpace(TId? fileId, string filename, long fileSize, string rootDirectoryPath)
    {
        if (_volumeInfoProvider.HasInsufficientFreeSpace(rootDirectoryPath, fileSize))
        {
            throw new FileSystemClientException<TId>(
                $"Not enough free space to download the file {filename} ({fileSize} B).",
                FileSystemErrorCode.FreeSpaceExceeded,
                fileId);
        }
    }
}
