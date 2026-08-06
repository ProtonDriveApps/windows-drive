namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

public interface IThumbnailProvider
{
    public const int MaxThumbnailNumberOfPixelsOnLargestSide = 512;
    public const int MaxHdPreviewNumberOfPixelsOnLargestSide = 1920;

    /// <summary>
    /// Obtains a thumbnail for a file.
    /// </summary>
    /// <returns>Thumbnail bytes if obtaining thumbnail succeeded; <see langword="null"/> otherwise.</returns>
    Task<ReadOnlyMemory<byte>?> TryGetThumbnailAsync(int numberOfPixelsOnLargestSide, int maxNumberOfBytes, CancellationToken cancellationToken);
}
