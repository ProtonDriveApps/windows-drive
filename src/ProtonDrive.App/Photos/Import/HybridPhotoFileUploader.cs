using ProtonDrive.Shared.Features;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.Photos.Import;

internal sealed class HybridPhotoFileUploader : IPhotoFileUploader
{
    private readonly IPhotoFileUploader _legacyUploader;
    private readonly IPhotoFileUploader _sdkUploader;
    private readonly IFeatureFlagProvider _featureFlagProvider;

    public HybridPhotoFileUploader(
        IPhotoFileUploader legacyUploader,
        IPhotoFileUploader sdkUploader,
        IFeatureFlagProvider featureFlagProvider)
    {
        _legacyUploader = legacyUploader;
        _sdkUploader = sdkUploader;
        _featureFlagProvider = featureFlagProvider;
    }

    public async Task<NodeInfo<string>> UploadFileAsync(string filePath, string parentLinkId, string? mainPhotoLinkId, CancellationToken cancellationToken)
    {
        var uploader = await GetUploaderAsync(cancellationToken).ConfigureAwait(false);

        return await uploader.UploadFileAsync(filePath, parentLinkId, mainPhotoLinkId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IPhotoFileUploader> GetUploaderAsync(CancellationToken cancellationToken)
    {
        return await _featureFlagProvider.IsEnabledAsync(Feature.DriveWindowsSdkUploadPhoto, cancellationToken).ConfigureAwait(false)
            ? _sdkUploader
            : _legacyUploader;
    }
}
