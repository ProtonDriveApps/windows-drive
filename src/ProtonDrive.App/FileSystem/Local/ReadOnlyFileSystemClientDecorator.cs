using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Local;

internal class ReadOnlyFileSystemClientDecorator : FileSystemClientDecoratorBase<long>
{
    public ReadOnlyFileSystemClientDecorator(IFileSystemClient<long> decoratedInstance)
        : base(decoratedInstance)
    {
    }

    public override Task<IDestinationRevision<long>> CreateFileAsync(
        NodeInfo<long> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        var readOnlyInfo = ToReadOnly(info);

        return base.CreateFileAsync(readOnlyInfo, tempFileName, thumbnailProvider, fileMetadataProvider, progressCallback, cancellationToken);
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
        var readOnlyInfo = ToReadOnly(info);

        return base.CreateRevisionAsync(
            readOnlyInfo,
            size,
            lastWriteTime,
            tempFileName,
            thumbnailProvider,
            fileMetadataProvider,
            progressCallback,
            cancellationToken);
    }

    public override Task DeleteAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        var readOnlyInfo = ToReadOnly(info);

        return base.DeleteAsync(readOnlyInfo, cancellationToken);
    }

    public override Task DeletePermanentlyAsync(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        var readOnlyInfo = ToReadOnly(info);

        return base.DeletePermanentlyAsync(readOnlyInfo, cancellationToken);
    }

    private NodeInfo<long> ToReadOnly(NodeInfo<long> nodeInfo)
    {
        if (nodeInfo.IsDirectory())
        {
            return nodeInfo;
        }

        return nodeInfo.Copy().WithAttributes(nodeInfo.Attributes | FileAttributes.ReadOnly);
    }
}
