using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Proton.Drive.Sdk.Sync.Windows.Interop;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Thumbnails;

internal static class Win32ThumbnailExtractionExtensions
{
    public static (HResult Result, Shell32.ISharedBitmap? Thumbnail) TryGetThumbnail(
        this Shell32.IThumbnailCache thumbnailCache,
        Shell32.IShellItem shellItem,
        int numberOfPixelsOnLargestSide)
    {
        const Shell32.WTS_FLAGS extractThumbnailFlags =
            Shell32.WTS_FLAGS.WTS_EXTRACTDONOTCACHE |
            Shell32.WTS_FLAGS.WTS_SCALETOREQUESTEDSIZE |
            Shell32.WTS_FLAGS.WTS_REQUIRESURROGATE;

        var resultHandle = thumbnailCache.GetThumbnail(
            shellItem,
            (uint)numberOfPixelsOnLargestSide,
            extractThumbnailFlags,
            out var thumbnail,
            out _,
            out _);

        return (resultHandle, thumbnail);
    }

    public static (HResult Result, Shell32.ISharedBitmap? Thumbnail) TryGetDeferredThumbnail(
        this Shell32.IThumbnailCache thumbnailCache,
        Shell32.IShellItem shellItem,
        int numberOfPixelsOnLargestSide)
    {
        const Shell32.WTS_FLAGS getDeferredThumbnailFlags =
            Shell32.WTS_FLAGS.WTS_INCACHEONLY |
            Shell32.WTS_FLAGS.WTS_SCALETOREQUESTEDSIZE;

        var resultHandle = thumbnailCache.GetThumbnail(
            shellItem,
            (uint)numberOfPixelsOnLargestSide,
            getDeferredThumbnailFlags,
            out var thumbnail,
            out _,
            out _);

        return (resultHandle, thumbnail);
    }

    public static bool ThumbnailExtractionIsPending(this HResult result)
    {
        return result.Value is HResult.Code.WTS_E_EXTRACTIONPENDING or HResult.Code.STG_E_FILENOTFOUND;
    }

    public static RenderTargetBitmap GetNonTransparentBitmap(this BitmapSource bitmap)
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

    public static byte[] EncodeToJpeg(this BitmapSource source, int qualityLevel)
    {
        using var stream = new MemoryStream();

        var encoder = new JpegBitmapEncoder
        {
            QualityLevel = qualityLevel,
            Frames = { BitmapFrame.Create(source) },
        };

        encoder.Save(stream);

        return stream.ToArray();
    }
}
