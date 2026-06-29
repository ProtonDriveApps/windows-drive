namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Metadata.GoogleTakeout;

internal static class GoogleTakeoutMetadataExtensions
{
    public static DateTimeOffset? GetCaptureTime(this GoogleTakeoutDateTimeContract? data)
    {
        var captureTime = (DateTimeOffset?)data;

        return FileMetadataValidator.IsValidCaptureTime(captureTime) ? captureTime : null;
    }

    public static (double Latitude, double Longitude)? GetGeoLocation(this GoogleTakeoutGeoDataContract? data)
    {
        if (data is null)
        {
            return null;
        }

        return FileMetadataValidator.IsValidGeoCoordinates(data.Latitude, data.Longitude)
            ? (data.Latitude.Value, data.Longitude.Value)
            : null;
    }
}
