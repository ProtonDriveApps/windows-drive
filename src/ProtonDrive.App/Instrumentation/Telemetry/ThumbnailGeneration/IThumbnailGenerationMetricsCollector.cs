using ProtonDrive.Client.Contracts;

namespace ProtonDrive.App.Instrumentation.Telemetry.ThumbnailGeneration;

public interface IThumbnailGenerationMetricsCollector
{
    void Enable();

    void Disable();

    void RecordMetric(
        ThumbnailGenerationResult result,
        ThumbnailGenerationMethod method,
        ThumbnailType type,
        string fileExtension,
        long fileSizeInBytes,
        TimeSpan duration);

    IReadOnlyCollection<ThumbnailGenerationStatistics> GetAndReset();
}
