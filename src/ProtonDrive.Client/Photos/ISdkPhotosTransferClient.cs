using Proton.Drive.Sdk.Nodes;

namespace ProtonDrive.Client.Photos;

internal interface ISdkPhotosTransferClient
{
    Task<string> UploadAsync(
        string filename,
        string mediaType,
        long size,
        PhotosFileUploadMetadata metadata,
        Stream contentStream,
        IEnumerable<Thumbnail> thumbnails,
        Func<ReadOnlyMemory<byte>>? expectedSha1Provider,
        CancellationToken cancellationToken);
}
