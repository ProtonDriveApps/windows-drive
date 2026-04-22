using System.Diagnostics;
using System.IO;
using ProtonDrive.App.Instrumentation.Telemetry.ThumbnailGeneration;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.Windows.Services;

internal sealed class TelemetryThumbnailGeneratorDecorator : IThumbnailGenerator
{
    private readonly IThumbnailGenerator _decoratedInstance;
    private readonly ThumbnailGenerationMethod _method;
    private readonly IThumbnailGenerationMetricsCollector _metricsCollector;

    public TelemetryThumbnailGeneratorDecorator(
        IThumbnailGenerator instanceToDecorate,
        ThumbnailGenerationMethod method,
        IThumbnailGenerationMetricsCollector metricsCollector)
    {
        _decoratedInstance = instanceToDecorate;
        _method = method;
        _metricsCollector = metricsCollector;
    }

    public async Task<ReadOnlyMemory<byte>?> TryGenerateThumbnailAsync(
        string filePath,
        int numberOfPixelsOnLargestSide,
        int maxNumberOfBytes,
        CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(filePath);

        var fileSize = fileInfo.Length;
        var type = ThumbnailGenerationExtensions.IsRequestingHdPreview(numberOfPixelsOnLargestSide)
            ? ThumbnailType.HdPreview
            : ThumbnailType.Preview;
        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

        var started = Stopwatch.GetTimestamp();

        try
        {
            var result = await _decoratedInstance.TryGenerateThumbnailAsync(filePath, numberOfPixelsOnLargestSide, maxNumberOfBytes, cancellationToken)
                .ConfigureAwait(false);

            if (result is not null)
            {
                _metricsCollector.RecordMetric(
                    ThumbnailGenerationResult.Success,
                    _method,
                    type,
                    fileExtension,
                    fileSize,
                    Stopwatch.GetElapsedTime(started));
            }

            return result;
        }
        catch (ThumbnailGenerationException ex) when (ex.ErrorCode == ThumbnailGenerationErrorCode.ExtensionNotSupported)
        {
            throw;
        }
        catch (ThumbnailGenerationException)
        {
            _metricsCollector.RecordMetric(
                ThumbnailGenerationResult.Failure,
                _method,
                type,
                fileExtension,
                fileSize,
                Stopwatch.GetElapsedTime(started));

            throw;
        }
    }
}
