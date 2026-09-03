using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Thumbnails;
using Proton.Drive.Sdk.Sync.Windows.Interop;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Media;
using Proton.Drive.Shared.Threading;
using Gdi32 = Proton.Drive.Sdk.Sync.Windows.Interop.Gdi32;
using Shell32 = Proton.Drive.Sdk.Sync.Windows.Interop.Shell32;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Thumbnails;

/// <summary>
/// Provides thumbnail generation for image files using Windows API
/// </summary>
// ReSharper disable InconsistentNaming
internal class Win32ThumbnailGenerator : IThumbnailGenerator
{
    private static readonly TimeSpan ThumbnailExtractionTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ThumbnailExtractionResultPollingInterval = TimeSpan.FromMilliseconds(500);

    // Below 15 it will start becoming unrecognizable and maybe even so ugly that using an icon rather than a thumbnail is likely to be more acceptable.
    private static readonly ImmutableArray<int> QualityLevels = [80, 70, 60, 45, 30, 15, 10, 5];

    // Polling for pending thumbnail retrieval result must be performed on the same thread on which thumbnail retrieval was requested.
    private static readonly DedicatedThreadTaskScheduler TaskScheduler = new();

    private readonly IClock _clock;
    private readonly ILogger<IThumbnailGenerator> _logger;

    private readonly Win32ThumbnailGenerationValidator _generationValidator;

    public Win32ThumbnailGenerator(
        IClock clock,
        ILogger<IThumbnailGenerator> logger)
    {
        _clock = clock;
        _logger = logger;

        _generationValidator = new Win32ThumbnailGenerationValidator(logger);
    }

    public async Task<ReadOnlyMemory<byte>?> TryGenerateThumbnailAsync(
        string filePath,
        int numberOfPixelsOnLargestSide,
        int maxNumberOfBytes,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath);
        var fileExtensionToLog = extension[^Math.Min(extension.Length, 5)..].ToLowerInvariant();

        if (!KnownFileExtensions.ImageExtensions.Contains(extension) && !KnownFileExtensions.VideoExtensions.Contains(extension))
        {
            _logger.LogDebug(
                "Thumbnail generation (Win32) skipped: File extension \"{Extension}\" not supported",
                fileExtensionToLog);

            throw new ThumbnailGenerationException(ThumbnailGenerationErrorCode.ExtensionNotSupported, $"File extension \"{fileExtensionToLog}\" not supported");
        }

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Thumbnail generation (Win32) failed: File not found");
            return null;
        }

        IntPtr hBitmap = IntPtr.Zero;

        try
        {
            var isHdPreview = ThumbnailGenerationExtensions.IsRequestingHdPreview(numberOfPixelsOnLargestSide);

            if (isHdPreview && !_generationValidator.IsHdPreviewAllowed(filePath))
            {
                return null;
            }

            (hBitmap, _) = await GetNativeBitmapHandleAsync(filePath, numberOfPixelsOnLargestSide, cancellationToken).ConfigureAwait(false);

            if (hBitmap == IntPtr.Zero)
            {
                throw new ThumbnailGenerationException(ThumbnailGenerationErrorCode.Failed, "Failed to get bitmap handle");
            }

            var bitmap = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            var nonTransparentBitmap = bitmap.GetNonTransparentBitmap();

            var qualityLevelIndex = 0;
            byte[] thumbnailBytes;
            do
            {
                thumbnailBytes = nonTransparentBitmap.EncodeToJpeg(QualityLevels[qualityLevelIndex++]);
            }
            while (thumbnailBytes.Length > maxNumberOfBytes && qualityLevelIndex < QualityLevels.Length);

            if (thumbnailBytes.Length > maxNumberOfBytes)
            {
                throw new ThumbnailGenerationException(ThumbnailGenerationErrorCode.Failed, $"Could not compress thumbnail into less than {maxNumberOfBytes} bytes");
            }

            _logger.LogDebug(
                "{ThumbnailType} generation (Win32) succeeded for file \"{FileName}\": {Width}x{Height} pixels, {Size} bytes",
                isHdPreview ? "HD preview" : "Thumbnail",
                Path.GetFileName(filePath),
                bitmap.PixelWidth,
                bitmap.PixelHeight,
                thumbnailBytes.Length);

            if (thumbnailBytes.Length == 0)
            {
                _logger.LogWarning(
                    "{ThumbnailType} generation (Win32) failed for file \"{FileName}\": Empty thumbnail",
                    isHdPreview ? "HD preview" : "Thumbnail",
                    fileExtensionToLog);
                throw new ThumbnailGenerationException(ThumbnailGenerationErrorCode.Failed, "Empty thumbnail");
            }

            return thumbnailBytes;
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning("Thumbnail generation (Win32) failed: File not found");
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var exceptionType = exception.GetType().Name;
            _logger.LogWarning(
                "Thumbnail generation (Win32) failed for file extension \"{Extension}\": {ExceptionType}: 0x{HResult:x8}",
                fileExtensionToLog,
                exceptionType,
                exception.HResult);

            throw new ThumbnailGenerationException(ThumbnailGenerationErrorCode.Failed, "Thumbnail generation failed", exception);
        }
        finally
        {
            Gdi32.DeleteObject(hBitmap);
        }
    }

    private static Task<TResult> Schedule<TResult>(Func<TResult> work, CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(
            work,
            cancellationToken,
            TaskCreationOptions.None,
            TaskScheduler);
    }

    private async Task<(IntPtr Handle, HResult HResult)> GetNativeBitmapHandleAsync(string fileName, int numberOfPixelsOnLargestSide, CancellationToken cancellationToken)
    {
        var itemGuid = Shell32.IID_IShellItem;

        var resultHandle = Shell32.SHCreateItemFromParsingName(fileName, IntPtr.Zero, ref itemGuid, out object item);

        resultHandle.ThrowOnFailure();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var shellItem = (Shell32.IShellItem)item;

            var thumbnailCache = Shell32.ThumbnailCache.GetInstance();

            try
            {
                return await GetThumbnailHandleAsync(thumbnailCache, shellItem, numberOfPixelsOnLargestSide, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Marshal.ReleaseComObject(thumbnailCache);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(item);
        }
    }

    private async Task<(IntPtr Handle, HResult HResult)> GetThumbnailHandleAsync(
        Shell32.IThumbnailCache thumbnailCache,
        Shell32.IShellItem shellItem,
        int numberOfPixelsOnLargestSide,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startTickCount = _clock.TickCount;

        var (result, thumbnail) = await Schedule(() => thumbnailCache.TryGetThumbnail(shellItem, numberOfPixelsOnLargestSide), cancellationToken).ConfigureAwait(false);

        while (result.ThumbnailExtractionIsPending() && (_clock.TickCount - startTickCount) < ThumbnailExtractionTimeout)
        {
            await Task.Delay(ThumbnailExtractionResultPollingInterval, cancellationToken).ConfigureAwait(false);

            (result, thumbnail) = await Schedule(() => thumbnailCache.TryGetDeferredThumbnail(shellItem, numberOfPixelsOnLargestSide), cancellationToken).ConfigureAwait(false);
        }

        if (result.Failed || thumbnail is null)
        {
            _logger.LogWarning("Thumbnail generation (Win32) of max {Size}px failed: 0x{ErrorCode:x8}", numberOfPixelsOnLargestSide, result.AsInt32);

            return (IntPtr.Zero, result);
        }

        var resultHandle = thumbnail.Detach(out var nativeBitmapHandle);
        resultHandle.ThrowOnFailure();

        Marshal.ReleaseComObject(thumbnail);

        return (nativeBitmapHandle, new HResult(HResult.Code.S_OK));
    }
}
