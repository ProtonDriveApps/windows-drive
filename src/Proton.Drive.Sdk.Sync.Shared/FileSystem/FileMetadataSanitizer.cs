using System.Drawing;

namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

public static class FileMetadataSanitizer
{
    public static FileMetadata? GetFileMetadata(
        int? width,
        int? height,
        double? durationInSeconds,
        int? cameraOrientation,
        string? cameraDevice,
        DateTimeOffset? captureTime,
        double? latitude,
        double? longitude)
    {
        if (!FileMetadataValidator.IsValid(width, height, durationInSeconds, cameraOrientation, cameraDevice, latitude, longitude))
        {
            return null;
        }

        var mediaSizeIsValid = FileMetadataValidator.IsValidMediaSize(width, height);
        var durationIsValid = FileMetadataValidator.IsValidDuration(durationInSeconds);
        var captureTimeIsValid = FileMetadataValidator.IsValidCaptureTime(captureTime);
        var cameraOrientationIsValid = FileMetadataValidator.IsValidCameraOrientation(cameraOrientation);
        var cameraDeviceIsValid = FileMetadataValidator.IsValidCameraDevice(cameraDevice);
        var geoLocationIsValid = FileMetadataValidator.IsValidGeoCoordinates(latitude, longitude);

        return new FileMetadata
        {
            MediaSize = mediaSizeIsValid ? new Size(width!.Value, height!.Value) : null,
            DurationInSeconds = durationIsValid ? durationInSeconds : null,
            CameraOrientation = cameraOrientationIsValid ? cameraOrientation : null,
            CameraDevice = cameraDeviceIsValid ? cameraDevice : null,
            CaptureTime = captureTimeIsValid ? captureTime?.ToUniversalTime() : null,
            Latitude = geoLocationIsValid ? latitude : null,
            Longitude = geoLocationIsValid ? longitude : null,
        };
    }
}
