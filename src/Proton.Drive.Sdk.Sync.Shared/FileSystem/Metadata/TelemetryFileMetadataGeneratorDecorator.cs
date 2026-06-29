using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Metadata.GoogleTakeout;
using Proton.Drive.Shared.Logging;
using Proton.Drive.Shared.Media;
using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Metadata;

public sealed class TelemetryFileMetadataGeneratorDecorator : IFileMetadataGenerator
{
    private readonly IFileMetadataGenerator _decoratedInstance;
    private readonly IErrorCounter _errorCounter;
    private readonly ILogger<TelemetryFileMetadataGeneratorDecorator> _logger;

    public TelemetryFileMetadataGeneratorDecorator(
        IFileMetadataGenerator instanceToDecorate,
        IErrorCounter errorCounter,
        ILogger<TelemetryFileMetadataGeneratorDecorator> logger)
    {
        _decoratedInstance = instanceToDecorate;
        _errorCounter = errorCounter;
        _logger = logger;
    }

    public async Task<FileMetadata?> GetMetadataAsync(string filePath)
    {
        var result = await _decoratedInstance.GetMetadataAsync(filePath).ConfigureAwait(false);

        if (result is null)
        {
            return result;
        }

        var fileExtension = Path.GetExtension(filePath);

        // CaptureTime is only meaningful for raster images (photos).
        // Videos, audio, and other file types are expected to have no capture time.
        if (!KnownFileExtensions.RasterImageExtensions.Contains(fileExtension))
        {
            return result;
        }

        FileMetadataExtractionErrorCode? errorCode = result.CaptureTime is null
            ? GoogleTakeoutPaths.IsFromGoogleTakeoutImport(filePath)
                ? FileMetadataExtractionErrorCode.MissingCaptureTimeGoogleTakeout
                : FileMetadataExtractionErrorCode.MissingCaptureTime
            : null;

        if (errorCode is not null)
        {
            _logger.LogWarning(
                "Capture time is missing for file \"{FilePath}\" (extension \"{Extension}\"): {ErrorCode}",
                _logger.GetSensitiveValueForLogging(filePath),
                fileExtension[^Math.Min(fileExtension.Length, 5)..].ToLowerInvariant(),
                errorCode);

            _errorCounter.Add(ErrorScope.PhotoImportItemOperation, new FileMetadataExtractionException($"Missing capture time: {errorCode}", errorCode.Value));
        }

        return result;
    }
}
