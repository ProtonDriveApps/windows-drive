using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Windows.Interop;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.Windows.Services;

public class Win32ThumbnailGenerator : IThumbnailGenerator
{
    // Below 15 it will start becoming unrecognizable and maybe even so ugly that using an icon rather than a thumbnail is likely to be more acceptable.
    private static readonly ImmutableArray<int> QualityLevels = [80, 70, 60, 45, 30, 15, 10, 5];

    private static readonly FrozenSet<string> SupportedExtensions = new HashSet<string>
    {
        // Images
        ".apng",
        ".bmp",
        ".gif",
        ".ico",
        ".vdnMicrosoftIcon",
        ".png",
        ".svg",
        ".jpg",
        ".jpeg",
        ".jpe",
        ".jif",
        ".jfif",
        ".tif",
        ".tiff",

        // Videos
        ".3gp",
        ".3gpp",
        ".3g2",
        ".h261",
        ".h263",
        ".h264",
        ".m4s",
        ".jpgv",
        ".jpm",
        ".jpgm",
        ".mj2",
        ".mjp2",
        ".ts",
        ".mp4",
        ".mp4v",
        ".mpg4",
        ".mpeg",
        ".mpg",
        ".mpe",
        ".m1v",
        ".m2v",
        ".ogv",
        ".qt",
        ".mov",
        ".uvh",
        ".uvvh",
        ".uvm",
        ".uvvm",
        ".uvp",
        ".uvvp",
        ".uvs",
        ".uvvs",
        ".uvv",
        ".uvvv",
        ".dvb",
        ".fvt",
        ".mxu",
        ".m4u",
        ".pyv",
        ".uvu",
        ".uvvu",
        ".viv",
        ".webm",
        ".f4v",
        ".fli",
        ".flv",
        ".m4v",
        ".mkv",
        ".mk3d",
        ".mks",
        ".mng",
        ".asf",
        ".asx",
        ".vob",
        ".wm",
        ".wmv",
        ".wmx",
        ".wvx",
        ".avi",
        ".movie",
        ".smv",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly ILogger<IThumbnailGenerator> _logger;

    public Win32ThumbnailGenerator(ILogger<IThumbnailGenerator> logger)
    {
        _logger = logger;
    }

    public bool TryGenerateThumbnail(string filePath, int numberOfPixelsOnLargestSide, int maxNumberOfBytes, out ReadOnlyMemory<byte> thumbnailBytes)
    {
        if (!SupportedExtensions.Contains(Path.GetExtension(filePath)))
        {
            _logger.LogInformation("Thumbnail generation skipped: file extension not supported");
            thumbnailBytes = ReadOnlyMemory<byte>.Empty;
            return false;
        }

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Thumbnail generation failed: File not found");
            thumbnailBytes = ReadOnlyMemory<byte>.Empty;
            return false;
        }

        IntPtr hBitmap = IntPtr.Zero;

        try
        {
            hBitmap = GetNativeBitmapHandle(filePath, numberOfPixelsOnLargestSide);

            if (hBitmap == IntPtr.Zero)
            {
                thumbnailBytes = ReadOnlyMemory<byte>.Empty;
                return false;
            }

            var bitmap = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var nonTransparentBitmap = GetNonTransparentBitmap(bitmap);

            var qualityLevelIndex = 0;
            do
            {
                thumbnailBytes = EncodeToJpeg(nonTransparentBitmap, QualityLevels[qualityLevelIndex++]);
            }
            while (thumbnailBytes.Length > maxNumberOfBytes && qualityLevelIndex < QualityLevels.Length);

            if (thumbnailBytes.Length > maxNumberOfBytes)
            {
                throw new ThumbnailGenerationException($"Could not generate thumbnail of less than {maxNumberOfBytes} bytes.");
            }

            _logger.LogInformation("Thumbnail generation succeeded");

            return true;
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning("Thumbnail generation failed: File not found");
            thumbnailBytes = ReadOnlyMemory<byte>.Empty;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Thumbnail generation failed: {ExceptionType}: {HResult}", ex.GetType().Name, ex.HResult);
            thumbnailBytes = ReadOnlyMemory<byte>.Empty;
            return false;
        }
        finally
        {
            Gdi32.DeleteObject(hBitmap);
        }
    }

    private static BitmapSource GetNonTransparentBitmap(BitmapSource bitmap)
    {
        var rect = new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
        var visual = new DrawingVisual();
        var context = visual.RenderOpen();
        context.DrawRectangle(new SolidColorBrush(Colors.Black), null, rect);
        context.DrawImage(bitmap, rect);
        context.Close();
        var render = new RenderTargetBitmap(bitmap.PixelWidth, bitmap.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        render.Render(visual);
        return render;
    }

    private static byte[] EncodeToJpeg(BitmapSource thumbnail, int qualityLevel)
    {
        using var stream = new MemoryStream();

        var encoder = new JpegBitmapEncoder
        {
            QualityLevel = qualityLevel,
            Frames = { BitmapFrame.Create(thumbnail) },
        };

        encoder.Save(stream);

        return stream.ToArray();
    }

    private IntPtr GetNativeBitmapHandle(string fileName, int numberOfPixelsOnLargestSide)
    {
        var itemGuid = Shell32.IID_IShellItem;

        var resultHandle = Shell32.SHCreateItemFromParsingName(fileName, IntPtr.Zero, ref itemGuid, out object item);

        resultHandle.ThrowOnFailure();

        try
        {
            var shellItem = (Shell32.IShellItem)item;

            var thumbnailCache = Shell32.ThumbnailCache.GetInstance();

            try
            {
                resultHandle = thumbnailCache.GetThumbnail(
                    shellItem,
                    (uint)numberOfPixelsOnLargestSide,
                    Shell32.WTS_FLAGS.WTS_EXTRACT | Shell32.WTS_FLAGS.WTS_EXTRACTDONOTCACHE | Shell32.WTS_FLAGS.WTS_SCALETOREQUESTEDSIZE,
                    out var sharedBitmap,
                    out _,
                    out _);

                if (resultHandle.Failed)
                {
                    _logger.LogWarning("Thumbnail generation failed: {ErrorCode}", resultHandle.AsInt32);

                    return IntPtr.Zero;
                }

                resultHandle = sharedBitmap.Detach(out var detachedBitmap);
                resultHandle.ThrowOnFailure();

                return detachedBitmap;
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
}
