using ProtonDrive.Sync.Shared.FileSystem.Thumbnails;

namespace ProtonDrive.Sync.Shared.Diagnostics.Metrics.Thumbnails;

public sealed record ThumbnailGenerationStatistics(
    int Count,
    ThumbnailGenerationResult Result,
    ThumbnailType Type,
    ThumbnailGenerationMethod Method,
    string FileExtension,
    ThumbnailGenerationFileSizeRange FileSizeRange,
    ThumbnailGenerationDurationRange DurationRange);
