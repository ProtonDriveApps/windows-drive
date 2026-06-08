using ProtonDrive.Client.Photos;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.Photos.Import;

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
