namespace ProtonDrive.Sync.Shared.FileSystem.Metadata.LivePhoto;

public sealed class LivePhotoMetadataExtractingDecorator : IFileMetadataGenerator
{
    private readonly IFileMetadataGenerator _decoratedInstance;
    private readonly ILivePhotoFileDetector _livePhotoFileDetector;

    public LivePhotoMetadataExtractingDecorator(IFileMetadataGenerator instanceToDecorate, ILivePhotoFileDetector livePhotoFileDetector)
    {
        _decoratedInstance = instanceToDecorate;
        _livePhotoFileDetector = livePhotoFileDetector;
    }

    public async Task<FileMetadata?> GetMetadataAsync(string filePath)
    {
        var metadata = await _decoratedInstance.GetMetadataAsync(filePath).ConfigureAwait(false);

        if (!_livePhotoFileDetector.TryGetMainLivePhotoPath(filePath, out var relatedPhotoFilePath))
        {
            return metadata;
        }

        var relatedPhotoMetadata = await _decoratedInstance.GetMetadataAsync(relatedPhotoFilePath).ConfigureAwait(false);

        if (relatedPhotoMetadata is null)
        {
            return metadata;
        }

        return (metadata ?? FileMetadata.Empty) with
        {
            CaptureTime = metadata?.CaptureTime ?? relatedPhotoMetadata.CaptureTime,
            CameraDevice = metadata?.CameraDevice ?? relatedPhotoMetadata.CameraDevice,
            CameraOrientation = metadata?.CameraOrientation ?? relatedPhotoMetadata.CameraOrientation,
            Latitude = metadata?.Latitude ?? relatedPhotoMetadata.Latitude,
            Longitude = metadata?.Longitude ?? relatedPhotoMetadata.Longitude,
        };
    }
}
