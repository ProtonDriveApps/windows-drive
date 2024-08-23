using System;

namespace ProtonDrive.Sync.Shared.FileSystem;

public interface IThumbnailProvider
{
    /// <summary>
    /// Attempts to obtain a file revision thumbnail.
    /// </summary>
    /// <returns><value>true</value> if the thumbnail has been obtained, <value>false</value> otherwise.</returns>
    bool TryGetThumbnail(int numberOfPixelsOnLargestSide, int maxNumberOfBytes, out ReadOnlyMemory<byte> thumbnailBytes);
}
