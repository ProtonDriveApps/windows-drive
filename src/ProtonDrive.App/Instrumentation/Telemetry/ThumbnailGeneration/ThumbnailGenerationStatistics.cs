using ProtonDrive.Client.Contracts;

namespace ProtonDrive.App.Instrumentation.Telemetry.ThumbnailGeneration;

public sealed record ThumbnailGenerationStatistics(
    int Count,
    ThumbnailGenerationResult Result,
    ThumbnailType Type,
    ThumbnailGenerationMethod Method,
    string FileExtension,
    ThumbnailGenerationFileSizeRange FileSizeRange,
    ThumbnailGenerationDurationRange DurationRange);
