using ProtonDrive.Client.Photos;
using ProtonDrive.Shared.Features;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.Photos.Import;

internal sealed class PhotoFileImporterFactory
{
    private readonly ISdkPhotosUploadClient _sdkPhotosUploadClient;
    private readonly IFeatureFlagProvider _featureFlagProvider;

    public PhotoFileImporterFactory(ISdkPhotosUploadClient sdkPhotosUploadClient, IFeatureFlagProvider featureFlagProvider)
    {
        _sdkPhotosUploadClient = sdkPhotosUploadClient;
        _featureFlagProvider = featureFlagProvider;
    }

    public IPhotoFileUploader Create(
        IPhotoFileSystemClient<long> localFileSystemClient,
        IFileSystemClient<string> remoteFileSystemClient,
        string photosVolumeId)
    {
        var legacyUploader = new PhotoFileUploader(localFileSystemClient, remoteFileSystemClient);
        var sdkUploader = new SdkPhotoFileUploader(localFileSystemClient, _sdkPhotosUploadClient, photosVolumeId);

        return new HybridPhotoFileUploader(legacyUploader, sdkUploader, _featureFlagProvider);
    }
}
