using ProtonDrive.Sync.Shared.FileSystem.Thumbnails;

namespace ProtonDrive.Sync.Shared.Diagnostics.Metrics.Thumbnails;

public interface IThumbnailGenerationMetricsCollector
{
    void Enable();

    void Disable();

    void RecordMetric(
        ThumbnailGenerationResult result,
        ThumbnailGenerationMethod method,
        ThumbnailType type,
        string fileExtension,
        long? fileSizeInBytes,
        TimeSpan duration);

    IReadOnlyCollection<ThumbnailGenerationStatistics> GetAndReset();
}
