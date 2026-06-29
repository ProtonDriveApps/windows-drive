using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Client.Photos;

public interface ISdkPhotosUploadClient
{
    Task<NodeInfo<string>> UploadAsync(
        string filename,
        ISourceRevision sourceRevision,
        string photosVolumeId,
        string? mainPhotoLinkId,
        CancellationToken cancellationToken);
}
