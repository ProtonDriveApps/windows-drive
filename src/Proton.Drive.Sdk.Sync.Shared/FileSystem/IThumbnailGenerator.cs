namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

public interface IThumbnailGenerator
{
    /// <summary>
    /// Obtains a thumbnail for a file.
    /// </summary>
    /// <returns>Thumbnail bytes if thumbnail extraction succeeded; <see langword="null"/> otherwise.</returns>
    Task<ReadOnlyMemory<byte>?> TryGenerateThumbnailAsync(
        string filePath,
        int numberOfPixelsOnLargestSide,
        int maxNumberOfBytes,
        CancellationToken cancellationToken);
}
