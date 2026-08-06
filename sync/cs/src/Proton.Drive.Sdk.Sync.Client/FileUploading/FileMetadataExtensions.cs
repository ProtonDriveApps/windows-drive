using System.Text.Json;
using Proton.Drive.Sdk.Nodes;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Client.FileUploading;

internal static class FileMetadataExtensions
{
    public static MediaExtendedAttributes? GetMediaExtendedAttributes(this FileMetadata metadata)
    {
        if (!FileMetadataValidator.IsValidMediaSize(metadata.MediaSize) && metadata.DurationInSeconds is null or 0)
        {
            return null;
        }

        return new MediaExtendedAttributes
        {
            Width = metadata.MediaSize?.Width,
            Height = metadata.MediaSize?.Height,
            Duration = metadata.DurationInSeconds,
        };
    }

    public static GeoLocationExtendedAttributes? GetLocationExtendedAttributes(this FileMetadata metadata)
    {
        if (metadata.Latitude is null || metadata.Longitude is null)
        {
            return null;
        }

        return new GeoLocationExtendedAttributes
        {
            Latitude = metadata.Latitude.Value,
            Longitude = metadata.Longitude.Value,
        };
    }

    public static CameraExtendedAttributes? GetCameraExtendedAttributes(this FileMetadata metadata)
    {
        if ((metadata.CameraOrientation is null or 0) && string.IsNullOrWhiteSpace(metadata.CameraDevice) && metadata.CaptureTime is null)
        {
            return null;
        }

        return new CameraExtendedAttributes
        {
            CaptureTime = metadata.CaptureTime,
            Device = metadata.CameraDevice,
            Orientation = metadata.CameraOrientation,
        };
    }

    public static IEnumerable<AdditionalMetadataProperty>? ConvertToAdditionalMetadataProperties(this FileMetadata? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        List<AdditionalMetadataProperty>? result = null;

        var cameraAttributes = metadata.GetCameraExtendedAttributes();
        if (cameraAttributes is not null)
        {
            result ??= new List<AdditionalMetadataProperty>(3);
            result.Add(new AdditionalMetadataProperty(nameof(ExtendedAttributes.Camera), JsonSerializer.SerializeToElement(cameraAttributes)));
        }

        var locationAttributes = metadata.GetLocationExtendedAttributes();
        if (locationAttributes is not null)
        {
            result ??= new List<AdditionalMetadataProperty>(2);
            result.Add(new AdditionalMetadataProperty(nameof(ExtendedAttributes.Location), JsonSerializer.SerializeToElement(locationAttributes)));
        }

        var mediaAttributes = metadata.GetMediaExtendedAttributes();
        if (mediaAttributes is not null)
        {
            result ??= new List<AdditionalMetadataProperty>(1);
            result.Add(new AdditionalMetadataProperty(nameof(ExtendedAttributes.Media), JsonSerializer.SerializeToElement(mediaAttributes)));
        }

        return result;
    }
}
