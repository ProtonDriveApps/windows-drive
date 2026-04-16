using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client.Photos;

public interface ISdkPhotosUploadClient
{
    Task<NodeInfo<string>> UploadAsync(
        string filename,
        ISourceRevision sourceRevision,
        string photosVolumeId,
        string? mainPhotoLinkId,
        CancellationToken cancellationToken);
}
