using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Shared.Diagnostics.Metrics.Thumbnails;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Thumbnails;

public sealed class TelemetryThumbnailGeneratorDecorator : IThumbnailGenerator
{
    private readonly IThumbnailGenerator _decoratedInstance;
    private readonly ThumbnailGenerationMethod _method;
    private readonly IThumbnailGenerationMetricsCollector _metricsCollector;
    private readonly ILogger<TelemetryThumbnailGeneratorDecorator> _logger;

    public TelemetryThumbnailGeneratorDecorator(
        IThumbnailGenerator instanceToDecorate,
        ThumbnailGenerationMethod method,
        IThumbnailGenerationMetricsCollector metricsCollector,
        ILogger<TelemetryThumbnailGeneratorDecorator> logger)
    {
        _decoratedInstance = instanceToDecorate;
        _method = method;
        _metricsCollector = metricsCollector;
        _logger = logger;
    }

    public async Task<ReadOnlyMemory<byte>?> TryGenerateThumbnailAsync(
        string filePath,
        int numberOfPixelsOnLargestSide,
        int maxNumberOfBytes,
        CancellationToken cancellationToken)
    {
        var type = ThumbnailGenerationExtensions.IsRequestingHdPreview(numberOfPixelsOnLargestSide)
            ? ThumbnailType.HdPreview
            : ThumbnailType.Preview;

        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

        var fileSize = GetFileSizeInBytes(filePath, fileExtension);

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

    private long? GetFileSizeInBytes(string filePath, string fileExtension)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);

            return fileInfo.Length;
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            _logger.LogWarning(
                "Reading size of file with extension \"{Extension}\" for thumbnail metrics failed: {ExceptionType}",
                fileExtension[^Math.Min(fileExtension.Length, 5)..].ToLowerInvariant(),
                ex.GetType().Name);

            return null;
        }
    }
}
