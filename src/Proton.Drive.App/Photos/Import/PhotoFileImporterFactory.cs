using Proton.Drive.Sdk.Sync.Client.Photos;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.App.Photos.Import;

internal sealed class PhotoFileImporterFactory
{
    private readonly ISdkPhotosUploadClient _sdkPhotosUploadClient;

    public PhotoFileImporterFactory(ISdkPhotosUploadClient sdkPhotosUploadClient)
    {
        _sdkPhotosUploadClient = sdkPhotosUploadClient;
    }

    public IPhotoFileUploader Create(
        IPhotoFileSystemClient<long> localFileSystemClient,
        string photosVolumeId)
    {
        return new SdkPhotoFileUploader(localFileSystemClient, _sdkPhotosUploadClient, photosVolumeId);
    }
}
