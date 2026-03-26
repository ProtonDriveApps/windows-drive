using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Remote;

internal sealed class FilteringSingleFileFileSystemClientDecorator : FileSystemClientDecoratorBase<string>
{
    public FilteringSingleFileFileSystemClientDecorator(IFileSystemClient<string> instanceToDecorate)
        : base(instanceToDecorate)
    {
    }

    public override Task<NodeInfo<string>> CreateDirectoryAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        throw GetException();
    }

    public override Task DeleteAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        throw GetException();
    }

    public override Task DeletePermanentlyAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        throw GetException();
    }

    public override Task<IDestinationRevision<string>> CreateFileAsync(
        NodeInfo<string> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        throw GetException();
    }

    public override Task MoveAsync(NodeInfo<string> info, NodeInfo<string> destinationInfo, CancellationToken cancellationToken)
    {
        throw GetException();
    }

    private static FileSystemClientException GetException()
    {
        return new FileSystemClientException(string.Empty, FileSystemErrorCode.MissingIndividuallySharedFile);
    }
}
