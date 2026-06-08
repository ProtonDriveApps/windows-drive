using ProtonDrive.Shared.Media;

namespace ProtonDrive.Sync.Shared.FileSystem.Thumbnails;

public static class ThumbnailGenerationExtensions
{
    private const int MinHdPreviewNumberOfPixelsOnLargestSide = IThumbnailProvider.MaxThumbnailNumberOfPixelsOnLargestSide + 1;

    public static bool IsRequestingHdPreview(int thumbnailNumberOfPixelsOnLargestSide)
    {
        return thumbnailNumberOfPixelsOnLargestSide > IThumbnailProvider.MaxThumbnailNumberOfPixelsOnLargestSide;
    }

    public static bool IsHdPreviewAllowed(int imageNumberOfPixelsOnLargestSide, string extension)
    {
        if (KnownFileExtensions.JpegExtensions.Contains(extension) || KnownFileExtensions.WebPImageExtensions.Contains(extension))
        {
            return imageNumberOfPixelsOnLargestSide > IThumbnailProvider.MaxHdPreviewNumberOfPixelsOnLargestSide;
        }

        return imageNumberOfPixelsOnLargestSide >= MinHdPreviewNumberOfPixelsOnLargestSide;
    }
}
