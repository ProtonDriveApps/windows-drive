using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Local;

internal sealed class VirtualFileRootFileSystemClientDecorator : FileSystemClientDecoratorBase<long>
{
    private readonly long _parentFolderId;
    private readonly string _rootFileName;

    public VirtualFileRootFileSystemClientDecorator(long parentFolderId, string rootFileName, IFileSystemClient<long> instanceToDecorate)
        : base(instanceToDecorate)
    {
        _parentFolderId = parentFolderId;
        _rootFileName = rootFileName;
    }

    public override void Connect(string syncRootPath, IFileHydrationDemandHandler<long> fileHydrationDemandHandler)
    {
        base.Connect(_rootFileName, fileHydrationDemandHandler);
    }

    public override Task<NodeInfo<long>> GetInfoAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        ValidateFile(info);

        return base.GetInfoAsync(info, cancellationToken);
    }

    public override IAsyncEnumerable<NodeInfo<long>> EnumerateAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        if (!IsRoot(info))
        {
            throw new FileSystemClientException("Unexpected folder in the file root", FileSystemErrorCode.ObjectNotFound);
        }

        return base.EnumerateAsync(info, cancellationToken).Where(x => x.IsFile() && x.Name.Equals(_rootFileName, StringComparison.Ordinal));
    }

    public override Task<NodeInfo<long>> CreateDirectoryAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        throw GetException();
    }

    public override Task<IDestinationRevision<long>> CreateFileAsync(
        NodeInfo<long> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        ValidateFile(info);

        return base.CreateFileAsync(info, tempFileName, thumbnailProvider, fileMetadataProvider, progressCallback, cancellationToken);
    }

    public override Task<ISourceRevision> OpenFileForReadingAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        ValidateFile(info);

        return base.OpenFileForReadingAsync(info, cancellationToken);
    }

    public override Task<IDestinationRevision<long>> CreateRevisionAsync(
        NodeInfo<long> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        ValidateFile(info);

        return base.CreateRevisionAsync(info, size, lastWriteTime, tempFileName, thumbnailProvider, fileMetadataProvider, progressCallback, cancellationToken);
    }

    public override Task MoveAsync(NodeInfo<long> info, NodeInfo<long> destinationInfo, CancellationToken cancellationToken)
    {
        throw GetException();
    }

    public override Task DeleteAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        throw GetException();
    }

    public override Task DeletePermanentlyAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        throw GetException();
    }

    public override Task DeleteRevisionAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        throw GetException();
    }

    public override void SetInSyncState(NodeInfo<long> info)
    {
        ValidateFile(info);

        base.SetInSyncState(info);
    }

    public override Task HydrateFileAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        ValidateFile(info);

        return base.HydrateFileAsync(info, cancellationToken);
    }

    private static bool IsDefault(long value) => value.Equals(0);

    private bool IsRoot(NodeInfo<long> info)
    {
        return (IsDefault(info.Id) && string.IsNullOrEmpty(info.Path)) ||
            (!IsDefault(info.Id) && info.Id.Equals(_parentFolderId));
    }

    private void ValidateFile(NodeInfo<long> info)
    {
        if (!IsDefault(info.ParentId) && !info.ParentId.Equals(_parentFolderId))
        {
            throw new FileSystemClientException($"Unexpected ParentId={info.ParentId}", FileSystemErrorCode.ObjectNotFound);
        }

        if (!string.Equals(info.Name, _rootFileName, StringComparison.Ordinal))
        {
            throw new FileSystemClientException("Unexpected file name", FileSystemErrorCode.PathNotFound);
        }
    }

    private static Exception GetException()
    {
        return new FileSystemClientException("Operation not supported on the virtual file root", FileSystemErrorCode.Unknown);
    }
}
