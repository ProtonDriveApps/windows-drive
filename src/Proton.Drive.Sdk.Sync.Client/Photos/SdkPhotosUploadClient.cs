using Proton.Drive.Sdk.Nodes;
using Proton.Drive.Sdk.Nodes.Upload;
using Proton.Drive.Sdk.Sync.Client.FileUploading;
using Proton.Drive.Sdk.Sync.Client.MediaTypes;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.Features;
using SdkPhotoTag = Proton.Drive.Sdk.Nodes.PhotoTag;

namespace Proton.Drive.Sdk.Sync.Client.Photos;

internal sealed class SdkPhotosUploadClient : ISdkPhotosUploadClient
{
    private readonly IFileContentTypeProvider _fileContentTypeProvider;
    private readonly ISdkPhotosTransferClient _sdkPhotosTransferClient;
    private readonly IFeatureFlagProvider _featureFlagProvider;

    public SdkPhotosUploadClient(
        IFileContentTypeProvider fileContentTypeProvider,
        ISdkPhotosTransferClient sdkPhotosTransferClient,
        IFeatureFlagProvider featureFlagProvider)
    {
        _fileContentTypeProvider = fileContentTypeProvider;
        _sdkPhotosTransferClient = sdkPhotosTransferClient;
        _featureFlagProvider = featureFlagProvider;
    }

    public async Task<NodeInfo<string>> UploadAsync(
        string filename,
        ISourceRevision sourceRevision,
        string photosVolumeId,
        string? mainPhotoLinkId,
        CancellationToken cancellationToken)
    {
        var mediaType = _fileContentTypeProvider.GetContentType(filename);
        var fileMetadata = await sourceRevision.GetMetadataAsync().ConfigureAwait(false);
        var photoTags = await sourceRevision.GetPhotoTagsAsync(cancellationToken).ConfigureAwait(false);
        var thumbnails = await sourceRevision.GetThumbnailsAsync(cancellationToken).ConfigureAwait(false);
        var mainPhotoUid = mainPhotoLinkId is not null
            ? NodeUid.Parse($"{photosVolumeId}~{mainPhotoLinkId}")
            : (NodeUid?)null;

        var metadata = new PhotosFileUploadMetadata
        {
            LastModificationTime = sourceRevision.LastWriteTimeUtc,
            CaptureTime = fileMetadata?.CaptureTime?.DateTime,
            AdditionalMetadata = fileMetadata.ConvertToAdditionalMetadataProperties(),
            Tags = photoTags.Count > 0 ? photoTags.Select(tag => (SdkPhotoTag)(int)tag) : null,
            MainPhotoUid = mainPhotoUid,
        };

        var checksumVerificationIsEnabled = await _featureFlagProvider.UploadChecksumVerificationIsEnabledAsync(cancellationToken).ConfigureAwait(false);

        var checksum = await sourceRevision.GetContentChecksumAsync(cancellationToken).ConfigureAwait(false);

        Func<ReadOnlyMemory<byte>>? expectedSha1Provider = checksum.Sha1 is { } sha1
            ? () => sha1
            : null;

        var sha1Digest = checksum.Sha1 is { } sha1Bytes ? Convert.ToHexStringLower(sha1Bytes.Span) : null;

        var contentStream = sourceRevision.GetContentStream();
        var size = sourceRevision.Size;

        try
        {
            var fileLinkId = await _sdkPhotosTransferClient.UploadAsync(
                filename,
                mediaType,
                size,
                metadata,
                contentStream,
                thumbnails,
                checksumVerificationIsEnabled ? expectedSha1Provider : null,
                cancellationToken).ConfigureAwait(false);

            return NodeInfo<string>.File()
                .WithId(fileLinkId)
                .WithName(filename)
                .WithSha1Digest(sha1Digest);
        }
        catch (ContentSizeMismatchIntegrityException exception)
        {
            var message = $"Failed to import photo due to file size mismatch: source {contentStream.Length} bytes, expected {size} bytes.";
            throw new PhotoFileSizeMismatchException(message, exception);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapSdkClientException(ex, id: null, includeObjectId: false, out var mappedException))
        {
            throw mappedException;
        }
    }
}
