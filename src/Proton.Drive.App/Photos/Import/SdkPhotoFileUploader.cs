using Proton.Drive.Sdk.Sync.Client.Photos;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.App.Photos.Import;

internal sealed class SdkPhotoFileUploader : IPhotoFileUploader
{
    private readonly IPhotoFileSystemClient<long> _localFileSystemClient;
    private readonly ISdkPhotosUploadClient _sdkPhotosUploadClient;
    private readonly string _photosVolumeId;

    public SdkPhotoFileUploader(
        IPhotoFileSystemClient<long> localFileSystemClient,
        ISdkPhotosUploadClient sdkPhotosUploadClient,
        string photosVolumeId)
    {
        _localFileSystemClient = localFileSystemClient;
        _sdkPhotosUploadClient = sdkPhotosUploadClient;
        _photosVolumeId = photosVolumeId;
    }

    public async Task<NodeInfo<string>> UploadFileAsync(string filePath, string parentLinkId, string? mainPhotoLinkId, CancellationToken cancellationToken)
    {
        var nodeInfo = NodeInfo<long>.File().WithPath(filePath);

        var sourceRevision = await _localFileSystemClient.OpenFileForReadingAsync(nodeInfo, cancellationToken).ConfigureAwait(false);

        await using (sourceRevision.ConfigureAwait(false))
        {
            return await _sdkPhotosUploadClient
                .UploadAsync(Path.GetFileName(filePath), sourceRevision, _photosVolumeId, mainPhotoLinkId, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
