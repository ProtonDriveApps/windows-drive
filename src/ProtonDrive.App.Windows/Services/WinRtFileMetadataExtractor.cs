using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Shared.FileSystem;
using Windows.Devices.Geolocation;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace ProtonDrive.App.Windows.Services;

/// <summary>
/// Base metadata extractor using Windows Runtime APIs.
/// Provides reliable extraction of basic properties for all media types,
/// and EXIF metadata for photos.
/// </summary>
internal sealed class WinRtFileMetadataExtractor : IFileMetadataGenerator
{
    private readonly ILogger<WinRtFileMetadataExtractor> _logger;

    public WinRtFileMetadataExtractor(ILogger<WinRtFileMetadataExtractor> logger)
    {
        _logger = logger;
    }

    public async Task<FileMetadata?> GetMetadataAsync(string filePath)
    {
        var file = await OpenFileAsync(filePath).ConfigureAwait(false);
        if (file == null)
        {
            return null;
        }

        var imageProperties = await GetImagePropertiesAsync(file).ConfigureAwait(false);
        if (imageProperties == null)
        {
            return null;
        }

        var width = (int)imageProperties.Width;
        var height = (int)imageProperties.Height;

        var cameraManufacturer = imageProperties.CameraManufacturer;
        var cameraModel = imageProperties.CameraModel;
        var captureTime = imageProperties.DateTaken;
        var cameraOrientation = (int)imageProperties.Orientation;
        var cameraDevice = (cameraManufacturer + " " + cameraModel).Trim();

        var videoProperties = await GetVideoPropertiesAsync(file).ConfigureAwait(false);
        if (videoProperties != null)
        {
            width = width != 0 ? width : (int)videoProperties.Width;
            height = height != 0 ? height : (int)videoProperties.Height;
        }

        var duration = double.Floor(videoProperties?.Duration.TotalSeconds ?? 0);

        if (duration == 0)
        {
            var musicProperties = await GetMusicPropertiesAsync(file).ConfigureAwait(false);
            if (musicProperties != null)
            {
                duration = double.Floor(musicProperties.Duration.TotalSeconds);
            }
        }

        var geoTag = await GetGeotagAsync(file).ConfigureAwait(false);
        var latitude = geoTag?.Position.Latitude;
        var longitude = geoTag?.Position.Longitude;

        return FileMetadataSanitizer.GetFileMetadata(
            width,
            height,
            duration,
            cameraOrientation,
            cameraDevice,
            captureTime,
            latitude,
            longitude);
    }

    private Task<StorageFile?> OpenFileAsync(string filePath)
    {
        return WithExceptionLogging(async () => await StorageFile.GetFileFromPathAsync(filePath), "Cannot open file");
    }

    private Task<ImageProperties?> GetImagePropertiesAsync(StorageFile file)
    {
        return WithExceptionLogging(async () => await file.Properties.GetImagePropertiesAsync(), "Cannot obtain image properties");
    }

    private Task<MusicProperties?> GetMusicPropertiesAsync(StorageFile file)
    {
        return WithExceptionLogging(async () => await file.Properties.GetMusicPropertiesAsync(), "Cannot obtain music properties");
    }

    private Task<VideoProperties?> GetVideoPropertiesAsync(StorageFile file)
    {
        return WithExceptionLogging(async () => await file.Properties.GetVideoPropertiesAsync(), "Cannot obtain video properties");
    }

    private Task<Geopoint?> GetGeotagAsync(StorageFile file)
    {
        return WithExceptionLogging(async () => await GeotagHelper.GetGeotagAsync(file), "Cannot obtain geotag");
    }

    private async Task<TResult?> WithExceptionLogging<TResult>(Func<Task<TResult>> function, string failureNote)
        where TResult : class
    {
        try
        {
            return await function.Invoke().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is COMException or ArgumentException)
        {
            _logger.LogWarning(
                "Metadata extraction failed: {FailureNote}: {ErrorCode} {ErrorMessage}",
                failureNote,
                ex.GetRelevantFormattedErrorCode(),
                ex.Message);
            return null;
        }
    }
}
