using System.Collections.Concurrent;
using Proton.Drive.Sdk.Sync.Shared.Diagnostics.Metrics.Thumbnails;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Thumbnails;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry.ThumbnailGeneration;

internal sealed class ThumbnailGenerationMetricsCollector : IThumbnailGenerationMetricsCollector
{
    private readonly ConcurrentDictionary<GenerationKey, int> _counts = new();

    private bool _isEnabled;

    public void Enable()
    {
        _isEnabled = true;
    }

    public void Disable()
    {
        _isEnabled = false;
    }

    public void RecordMetric(
        ThumbnailGenerationResult result,
        ThumbnailGenerationMethod method,
        ThumbnailType type,
        string fileExtension,
        long? fileSizeInBytes,
        TimeSpan duration)
    {
        if (!_isEnabled)
        {
            return;
        }

        var key = new GenerationKey(result, method, type, fileExtension, GetFileSizeRange(fileSizeInBytes), GetDurationRange(duration));

        _counts.AddOrUpdate(key, addValue: 1, updateValueFactory: static (_, current) => current + 1);
    }

    public IReadOnlyCollection<ThumbnailGenerationStatistics> GetAndReset()
    {
        var statistics = new List<ThumbnailGenerationStatistics>();

        foreach (var key in _counts.Keys)
        {
            if (_counts.TryRemove(key, out var count))
            {
                statistics.Add(new ThumbnailGenerationStatistics(
                    count,
                    key.Result,
                    key.Type,
                    key.Method,
                    key.FileExtension,
                    key.FileSizeRange,
                    key.DurationRange));
            }
        }

        return statistics;
    }

    private static ThumbnailGenerationFileSizeRange GetFileSizeRange(long? fileSizeInBytes) => fileSizeInBytes switch
    {
        null => ThumbnailGenerationFileSizeRange.NotAvailable,
        < 100L * 1024 => ThumbnailGenerationFileSizeRange.LessThan100KiB,
        < 1024L * 1024 => ThumbnailGenerationFileSizeRange.LessThan1MiB,
        < 10L * 1024 * 1024 => ThumbnailGenerationFileSizeRange.LessThan10MiB,
        < 100L * 1024 * 1024 => ThumbnailGenerationFileSizeRange.LessThan100MiB,
        < 1024L * 1024 * 1024 => ThumbnailGenerationFileSizeRange.LessThan1GiB,
        _ => ThumbnailGenerationFileSizeRange.AtLeast1GiB,
    };

    private static ThumbnailGenerationDurationRange GetDurationRange(TimeSpan duration) => duration.TotalMilliseconds switch
    {
        < 400 => ThumbnailGenerationDurationRange.LessThan400Ms,
        < 1_000 => ThumbnailGenerationDurationRange.LessThan1Sec,
        < 3_000 => ThumbnailGenerationDurationRange.LessThan3Sec,
        < 10_000 => ThumbnailGenerationDurationRange.LessThan10Sec,
        < 30_000 => ThumbnailGenerationDurationRange.LessThan30Sec,
        _ => ThumbnailGenerationDurationRange.AtLeast30Sec,
    };

    private readonly record struct GenerationKey(
        ThumbnailGenerationResult Result,
        ThumbnailGenerationMethod Method,
        ThumbnailType Type,
        string FileExtension,
        ThumbnailGenerationFileSizeRange FileSizeRange,
        ThumbnailGenerationDurationRange DurationRange);
}
