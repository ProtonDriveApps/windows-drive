namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Metadata.LivePhoto;

public sealed class LivePhotoThumbnailExtractingDecorator : IThumbnailGenerator
{
    private readonly ILivePhotoFileDetector _livePhotoFileDetector;
    private readonly IThumbnailGenerator _decoratedInstance;

    public LivePhotoThumbnailExtractingDecorator(ILivePhotoFileDetector livePhotoFileDetector, IThumbnailGenerator instanceToDecorate)
    {
        _livePhotoFileDetector = livePhotoFileDetector;
        _decoratedInstance = instanceToDecorate;
    }

    public async Task<ReadOnlyMemory<byte>?> TryGenerateThumbnailAsync(
        string filePath,
        int numberOfPixelsOnLargestSide,
        int maxNumberOfBytes,
        CancellationToken cancellationToken)
    {
        var thumbnail = await _decoratedInstance.TryGenerateThumbnailAsync(
            filePath,
            numberOfPixelsOnLargestSide,
            maxNumberOfBytes,
            cancellationToken).ConfigureAwait(false);

        if (thumbnail != null)
        {
            return thumbnail;
        }

        if (_livePhotoFileDetector.TryGetMainLivePhotoPath(filePath, out var relatedPhotoFilePath))
        {
            return await _decoratedInstance.TryGenerateThumbnailAsync(
                relatedPhotoFilePath,
                numberOfPixelsOnLargestSide,
                maxNumberOfBytes,
                cancellationToken).ConfigureAwait(false);
        }

        return thumbnail;
    }
}
