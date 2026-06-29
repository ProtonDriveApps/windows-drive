using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Thumbnails;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Logging;
using Proton.Drive.Shared.Media;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Thumbnails;

internal sealed class Win32ThumbnailGenerationValidator
{
    private readonly ILogger<IThumbnailGenerator> _logger;

    public Win32ThumbnailGenerationValidator(ILogger<IThumbnailGenerator> logger)
    {
        _logger = logger;
    }

    public bool IsHdPreviewAllowed(string filePath)
    {
        var extension = Path.GetExtension(filePath);

        // We skip large thumbnail generation for vector graphics images, because we cannot obtain image dimensions without generating the thumbnail
        if (!KnownFileExtensions.RasterImageExtensions.Contains(extension))
        {
            _logger.LogInformation(
                "HD preview generation skipped: File extension \"{Extension}\" not supported",
                extension[^Math.Min(extension.Length, 5)..].ToLowerInvariant());
            return false;
        }

        var imageNumberOfPixelsOnLargestSide = GetNumberOfPixelsOnLargestSide(filePath);

        return ThumbnailGenerationExtensions.IsHdPreviewAllowed(imageNumberOfPixelsOnLargestSide, extension);
    }

    private int GetNumberOfPixelsOnLargestSide(string filePath)
    {
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);

            return Math.Max(decoder.Frames[0].PixelWidth, decoder.Frames[0].PixelHeight);
        }
        catch (Exception ex) when (ex is ArgumentException or COMException or FileFormatException or InvalidOperationException or NotSupportedException)
        {
            var (fileName, extension) = GetValuesForLogging(filePath);

            _logger.LogWarning(
                "HD preview generation (Win32) for file \"{File}\" with extension \"{Extension}\" failed: File format not supported : {ErrorCode}",
                fileName,
                extension,
                ex.GetRelevantFormattedErrorCode());

            throw new ThumbnailGenerationException(ThumbnailGenerationErrorCode.Failed);
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            var (fileName, extension) = GetValuesForLogging(filePath);

            _logger.LogWarning(
                "HD preview generation (Win32) for file \"{File}\" with extension \"{Extension}\" failed: {ExceptionType} : {ErrorCode}",
                fileName,
                extension,
                ex.GetType().Name,
                ex.GetRelevantFormattedErrorCode());

            throw new ThumbnailGenerationException(ThumbnailGenerationErrorCode.Failed);
        }
    }

    private (string FileNameForLogging, string FileExtensionForLogging) GetValuesForLogging(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(fileName);

        return (_logger.GetSensitiveValueForLogging(fileName), extension[^Math.Min(extension.Length, 5)..].ToLowerInvariant());
    }
}
