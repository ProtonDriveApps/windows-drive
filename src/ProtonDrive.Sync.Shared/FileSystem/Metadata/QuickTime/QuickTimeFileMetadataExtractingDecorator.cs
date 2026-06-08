namespace ProtonDrive.Sync.Shared.FileSystem.Metadata.QuickTime;

/// <summary>
/// Decorator that enhances WinRT metadata with QuickTime-specific metadata for MP4 videos.
/// This decorator only processes QuickTime-compatible video formats.
/// </summary>
public sealed class QuickTimeFileMetadataExtractingDecorator : IFileMetadataGenerator
{
    private readonly IFileMetadataGenerator _decoratedInstance;
    private readonly QuickTimeFileMetadataExtractor _quickTimeMetadataExtractor;

    public QuickTimeFileMetadataExtractingDecorator(
        IFileMetadataGenerator instanceToDecorate,
        QuickTimeFileMetadataExtractor quickTimeMetadataExtractor)
    {
        _decoratedInstance = instanceToDecorate;
        _quickTimeMetadataExtractor = quickTimeMetadataExtractor;
    }

    public async Task<FileMetadata?> GetMetadataAsync(string filePath)
    {
        var baseMetadata = await _decoratedInstance.GetMetadataAsync(filePath).ConfigureAwait(false);

        var quickTimeFileMetadata = _quickTimeMetadataExtractor.GetMetadata(filePath);

        return MergeMetadata(baseMetadata, quickTimeFileMetadata);
    }

    private static FileMetadata? MergeMetadata(FileMetadata? baseMetadata, FileMetadata? quickTimeFileMetadata)
    {
        if (quickTimeFileMetadata == null)
        {
            return baseMetadata;
        }

        if (baseMetadata == null)
        {
            return quickTimeFileMetadata;
        }

        return new FileMetadata
        {
            // Keep existing properties
            MediaSize = baseMetadata.MediaSize,
            DurationInSeconds = baseMetadata.DurationInSeconds,
            Latitude = baseMetadata.Latitude,
            Longitude = baseMetadata.Longitude,

            // Prefer QuickTime properties
            CaptureTime = quickTimeFileMetadata.CaptureTime ?? baseMetadata.CaptureTime,
            CameraDevice = quickTimeFileMetadata.CameraDevice ?? baseMetadata.CameraDevice,
            CameraOrientation = quickTimeFileMetadata.CameraOrientation ?? baseMetadata.CameraOrientation,
        };
    }
}
