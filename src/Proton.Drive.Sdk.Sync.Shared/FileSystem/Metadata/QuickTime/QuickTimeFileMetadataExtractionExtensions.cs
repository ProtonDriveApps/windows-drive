using MetadataExtractor;
using MetadataExtractor.Formats.QuickTime;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Metadata.QuickTime;

internal static class QuickTimeFileMetadataExtractionExtensions
{
    public static FileMetadata? ExtractQuickTimeMetadata(this IReadOnlyList<MetadataExtractor.Directory> metadata)
    {
        string? cameraManufacturer = null;
        string? cameraModel = null;
        DateTimeOffset? captureTime = null;
        int? orientation = null;

        foreach (var directory in metadata)
        {
            switch (directory)
            {
                case QuickTimeMovieHeaderDirectory movieHeader:
                    if (captureTime == null
                        && movieHeader.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out var creationTime)
                        && creationTime.ToUnixTimeSeconds() > 0)
                    {
                        if (creationTime.Kind is DateTimeKind.Unspecified)
                        {
                            creationTime = DateTime.SpecifyKind(creationTime, DateTimeKind.Utc);
                        }

                        captureTime = new DateTimeOffset(creationTime);
                    }

                    break;

                case QuickTimeMetadataHeaderDirectory metadataHeader:
                    // Android device info
                    cameraManufacturer ??= metadataHeader.GetDescription(QuickTimeMetadataHeaderDirectory.TagAndroidManufacturer)?.Trim();
                    cameraModel ??= metadataHeader.GetDescription(QuickTimeMetadataHeaderDirectory.TagAndroidModel)?.Trim();

                    // iOS and generic device info
                    cameraManufacturer ??= metadataHeader.GetDescription(QuickTimeMetadataHeaderDirectory.TagMake)?.Trim();
                    cameraModel ??= metadataHeader.GetDescription(QuickTimeMetadataHeaderDirectory.TagModel)?.Trim();
                    break;

                case QuickTimeTrackHeaderDirectory trackHeader:
                    if (orientation == null && trackHeader.TryGetInt32(QuickTimeTrackHeaderDirectory.TagRotation, out var rotation))
                    {
                        orientation = ConvertRotationToOrientation(rotation);
                    }

                    break;
            }

            var parsingIsComplete = captureTime.HasValue
                && !string.IsNullOrEmpty(cameraManufacturer)
                && !string.IsNullOrEmpty(cameraModel)
                && orientation.HasValue;

            if (parsingIsComplete)
            {
                break;
            }
        }

        var cameraDevice = (cameraManufacturer + " " + cameraModel).Trim();

        return FileMetadataSanitizer.GetFileMetadata(
            width: null,
            height: null,
            durationInSeconds: null,
            orientation,
            cameraDevice,
            captureTime,
            latitude: null,
            longitude: null);
    }

    private static int ConvertRotationToOrientation(int rotation)
    {
        return rotation switch
        {
            0 => 1,          // Normal
            90 => 6,         // Rotate 90 CW
            180 => 3,        // Rotate 180
            -90 or 270 => 8, // Rotate 90 CCW
            _ => 1,          // Default to normal
        };
    }
}
