using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Media;
using ProtonDrive.Shared.Reporting;
using SkiaSharp;

namespace ProtonDrive.Sync.Shared.FileSystem.Thumbnails;

/// <summary>
/// Provides cross-platform thumbnail generation for image files using SkiaSharp.
/// </summary>
public sealed class SkiaThumbnailGenerator : IThumbnailGenerator
{
    private readonly IErrorReporting _errorReporting;
    private readonly ILogger<IThumbnailGenerator> _logger;

    public SkiaThumbnailGenerator(IErrorReporting errorReporting, ILogger<IThumbnailGenerator> logger)
    {
        _errorReporting = errorReporting;
        _logger = logger;
    }

    public Task<ReadOnlyMemory<byte>?> TryGenerateThumbnailAsync(
        string filePath,
        int numberOfPixelsOnLargestSide,
        int maxNumberOfBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(TryGenerateThumbnail(filePath, numberOfPixelsOnLargestSide, maxNumberOfBytes, cancellationToken));
    }

    private ReadOnlyMemory<byte>? TryGenerateThumbnail(
        string filePath,
        int numberOfPixelsOnLargestSide,
        int maxNumberOfBytes,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath);
        var fileExtensionToLog = extension[^Math.Min(extension.Length, 5)..].ToLowerInvariant();

        if (!KnownFileExtensions.RasterImageExtensions.Contains(extension))
        {
            _logger.LogDebug(
                "Thumbnail generation (Skia) skipped: File extension \"{Extension}\" not supported",
                fileExtensionToLog);

            throw new ThumbnailGenerationException(ThumbnailGenerationErrorCode.ExtensionNotSupported, $"File extension \"{fileExtensionToLog}\" not supported");
        }

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Thumbnail generation (Skia) failed: File not found");
            return null;
        }

        try
        {
            return GenerateCompressedThumbnail(filePath, extension, numberOfPixelsOnLargestSide, maxNumberOfBytes, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning("Thumbnail generation (Skia) failed: File not found");
            return null;
        }
        catch (ThumbnailGenerationException exception)
        {
            _logger.LogWarning(
                "Thumbnail generation (Skia) failed for file extension \"{Extension}\": {ErrorMessage}",
                fileExtensionToLog,
                exception.Message);

            ReportError(fileExtensionToLog, numberOfPixelsOnLargestSide, exception.Message);
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var exceptionType = exception.GetType().Name;
            _logger.LogWarning(
                exception,
                "Thumbnail generation (Skia) failed for file extension \"{Extension}\": {ExceptionType}: {HResult}",
                fileExtensionToLog,
                exceptionType,
                exception.HResult);

            ReportError(fileExtensionToLog, numberOfPixelsOnLargestSide, exceptionType, exception.HResult);
            throw new ThumbnailGenerationException(ThumbnailGenerationErrorCode.Failed, "Thumbnail generation failed", exception);
        }
    }

    private ReadOnlyMemory<byte>? GenerateCompressedThumbnail(
        string filePath,
        string extension,
        int numberOfPixelsOnLargestSide,
        int maxSizeInBytes,
        CancellationToken cancellationToken)
    {
        using var inputImageStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        using var skiaStream = new SKManagedStream(inputImageStream, disposeManagedStream: false);

        using var skiaCodec = SKCodec.Create(skiaStream, out var codecResult);

        if (skiaCodec is null)
        {
            throw new ThumbnailGenerationException(ThumbnailGenerationErrorCode.FormatNotSupported, $"Failed to decode image: {codecResult}");
        }

        var originalInfo = skiaCodec.Info;

        var isHdPreview = ThumbnailGenerationExtensions.IsRequestingHdPreview(numberOfPixelsOnLargestSide);

        if (isHdPreview)
        {
            var imageNumberOfPixelsOnLargestSide = Math.Max(originalInfo.Height, originalInfo.Width);

            if (!ThumbnailGenerationExtensions.IsHdPreviewAllowed(imageNumberOfPixelsOnLargestSide, extension))
            {
                return null;
            }
        }

        using var originalBitmap = SKBitmap.Decode(skiaCodec);

        if (originalBitmap is null)
        {
            throw new ThumbnailGenerationException("Failed to decode image");
        }

        if (originalBitmap.Width <= 0 || originalBitmap.Height <= 0)
        {
            throw new ThumbnailGenerationException($"Invalid image dimensions: {originalBitmap.Width}x{originalBitmap.Height}");
        }

        var aspectRatio = (float)originalBitmap.Width / originalBitmap.Height;
        var targetWidth = numberOfPixelsOnLargestSide;
        var targetHeight = numberOfPixelsOnLargestSide;

        // Preserve aspect ratio while fitting within max dimensions
        if (aspectRatio >= 1)
        {
            // Landscape or square: constrain by width
            targetHeight = (int)(targetWidth / aspectRatio);
        }
        else
        {
            // Portrait: constrain by height
            targetWidth = (int)(targetHeight * aspectRatio);
        }

        using var finalBitmap = originalBitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKSamplingOptions.Default);

        if (finalBitmap is null)
        {
            throw new ThumbnailGenerationException(ThumbnailGenerationErrorCode.Failed, "Failed to resize image");
        }

        using var image = SKImage.FromBitmap(finalBitmap);

        if (image is null)
        {
            throw new ThumbnailGenerationException(ThumbnailGenerationErrorCode.Failed, "Failed to create SKImage from resized bitmap");
        }

        var qualityLevel = 90;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var encodedData = image.Encode(SKEncodedImageFormat.Webp, qualityLevel);

            if (encodedData is null)
            {
                throw new ThumbnailGenerationException(ThumbnailGenerationErrorCode.Failed, "Failed to encode thumbnail to WebP format");
            }

            if (encodedData.Size > maxSizeInBytes)
            {
                qualityLevel -= 20;
                continue;
            }

            _logger.LogDebug(
                "Thumbnail compressed successfully: {Width}x{Height} pixels, {Size} bytes, quality {Quality}",
                targetWidth,
                targetHeight,
                encodedData.Size,
                qualityLevel);

            return encodedData.ToArray();
        }
        while (qualityLevel > 0);

        throw new ThumbnailGenerationException(ThumbnailGenerationErrorCode.Failed, $"Could not compress thumbnail into less than {maxSizeInBytes} bytes");
    }

    private void ReportError(string fileExtensionToReport, int requestedSize, string details, int? hresult = null)
    {
        var errorMessage =
            $"Thumbnail generation (Skia) with size {requestedSize} " +
            $"failed for \"{fileExtensionToReport}\": {(hresult.HasValue ? $"(0x{(uint)hresult.Value:x8}) {details}" : details)}";

        _errorReporting.CaptureError(errorMessage);
    }
}
