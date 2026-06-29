using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

public static class FileMetadataValidator
{
    private static readonly DateTimeOffset MinCaptureTime = new(new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    public static bool IsValidMediaSize(Size? mediaSize)
    {
        return mediaSize is { Width: > 0, Height: > 0 };
    }

    public static bool IsValidMediaSize(int? widthInPixels, int? heightInPixels)
    {
        return widthInPixels > 0 && heightInPixels > 0;
    }

    public static bool IsValidCaptureTime(DateTimeOffset? captureTime)
    {
        // For some photos with no capture time recorded, we still get the Unix epoch date with time zone offset of the current computer
        return captureTime > MinCaptureTime &&
            captureTime.Value.DateTime != DateTime.UnixEpoch;
    }

    public static bool IsValidDuration(double? durationInSeconds)
    {
        return durationInSeconds > 0;
    }

    public static bool IsValidCameraOrientation(int? cameraOrientation)
    {
        return cameraOrientation >= 0;
    }

    public static bool IsValidCameraDevice(string? cameraDevice)
    {
        return !string.IsNullOrWhiteSpace(cameraDevice);
    }

    public static bool IsValidGeoCoordinates([NotNullWhen(true)] double? latitude, [NotNullWhen(true)] double? longitude)
    {
        return latitude is not null && Math.Abs(latitude.Value) <= 90 && longitude is not null && Math.Abs(longitude.Value) <= 180;
    }

    public static bool IsValid(
        int? widthInPixels,
        int? heightInPixels,
        double? durationInSeconds,
        int? cameraOrientation,
        string? cameraDevice,
        double? latitude,
        double? longitude)
    {
        return IsValidMediaSize(widthInPixels, heightInPixels)
            || durationInSeconds > 0
            || cameraOrientation > 0
            || !string.IsNullOrWhiteSpace(cameraDevice)
            || (latitude is not null && longitude is not null);
    }
}
