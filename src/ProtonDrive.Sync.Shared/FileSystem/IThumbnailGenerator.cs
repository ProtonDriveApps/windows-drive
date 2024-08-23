using System;

namespace ProtonDrive.Sync.Shared.FileSystem;

public interface IThumbnailGenerator
{
    bool TryGenerateThumbnail(string filePath, int numberOfPixelsOnLargestSide, int maxNumberOfBytes, out ReadOnlyMemory<byte> thumbnailBytes);
}
