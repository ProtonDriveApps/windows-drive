using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Nodes;
using ProtonDrive.Client.Configuration;
using ProtonDrive.Client.Sdk;

namespace ProtonDrive.Client.Photos;

internal sealed class SdkPhotosTransferClient : ISdkPhotosTransferClient
{
    private readonly DriveApiConfig _apiConfig;
    private readonly ISdkPhotosClientFactory _sdkPhotosClientFactory;
    private readonly ILogger<SdkPhotosTransferClient> _logger;

    public SdkPhotosTransferClient(
        DriveApiConfig apiConfig,
        ISdkPhotosClientFactory sdkPhotosClientFactory,
        ILogger<SdkPhotosTransferClient> logger)
    {
        _apiConfig = apiConfig;
        _sdkPhotosClientFactory = sdkPhotosClientFactory;
        _logger = logger;
    }

    public async Task<string> UploadAsync(
        string filename,
        string mediaType,
        long size,
        PhotosFileUploadMetadata metadata,
        Stream contentStream,
        IEnumerable<Thumbnail> thumbnails,
        Func<ReadOnlyMemory<byte>>? expectedSha1Provider,
        CancellationToken cancellationToken)
    {
        var photosClient = _sdkPhotosClientFactory.GetOrCreatePhotosClient();

        var fileUploader = await photosClient.GetFileUploaderAsync(
            filename,
            mediaType,
            size,
            metadata,
            overrideExistingDraftByOtherClient: false,
            cancellationToken).ConfigureAwait(false);

        using (fileUploader)
        {
            var controller = fileUploader.UploadFromStream(
                contentStream,
                thumbnails,
                onProgress: null,
                expectedSha1Provider,
                forPhotos: true,
                cancellationToken);

            await using (controller.ConfigureAwait(false))
            {
                await controller.ExecuteWithRetryAsync(
                    _apiConfig.FileTransferNumberOfRetries,
                    _apiConfig.FileTransferDelayBetweenRetries,
                    _logger,
                    cancellationToken).ConfigureAwait(false);

                var (fileNodeUid, _) = await controller.Completion.ConfigureAwait(false);
                return fileNodeUid.ToLinkId();
            }
        }
    }
}
