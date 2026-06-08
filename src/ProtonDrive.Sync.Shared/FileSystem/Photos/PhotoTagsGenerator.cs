using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Xmp;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Media;
using ProtonDrive.Sync.Shared.FileSystem.Metadata.LivePhoto;
using XmpCore;
using Directory = MetadataExtractor.Directory;

namespace ProtonDrive.Sync.Shared.FileSystem.Photos;

/// <summary>
/// Generates a set of Photo tags based on media file metadata.
/// </summary>
/// <remarks>
/// Does not support reading metadata for these file formats:
/// <list type="bullet">
/// <item>Canon CRW</item>
/// <item>Kodak RAW</item>
/// <item>Pentax RAW</item>
/// <item>Sigma X3F</item>
/// </list>
/// </remarks>
public class PhotoTagsGenerator : IPhotoTagsGenerator
{
    // XMP namespace URIs
    private const string GoogleCamera = "http://ns.google.com/photos/1.0/camera/";
    private const string GooglePanorama = "http://ns.google.com/photos/1.0/panorama/";

    // XMP property names
    private const string ProjectionTypePropertyName = "ProjectionType";
    private const string MotionPhotoPropertyName = "MotionPhoto";

    // Metadata property values
    private const string ProjectionTypeValueEquirectangular = "equirectangular";
    private const string UserCommentValueScreenshot = "Screenshot";
    private const string LensModelValueFragmentFront = "front";

    private readonly ILivePhotoFileDetector _livePhotoFileDetector;
    private readonly ILogger<PhotoTagsGenerator> _logger;

    public PhotoTagsGenerator(ILivePhotoFileDetector livePhotoFileDetector, ILogger<PhotoTagsGenerator> logger)
    {
        _livePhotoFileDetector = livePhotoFileDetector;
        _logger = logger;
    }

    public Task<IReadOnlySet<PhotoTag>> GetPhotoTagsAsync(string filePath, CancellationToken cancellationToken)
    {
        var metadata = ReadMetadata(filePath);

        cancellationToken.ThrowIfCancellationRequested();

        var tags = new HashSet<PhotoTag>()
            .AddIf(PhotoTag.Screenshot, IsScreenshot(metadata))
            .AddIf(PhotoTag.Video, IsVideo(filePath))
            .AddIf(PhotoTag.MotionPhoto, IsMotionPhoto(metadata))
            .AddIf(PhotoTag.Selfie, IsSelfie(metadata))
            .AddIf(PhotoTag.Portrait, IsPortrait(metadata))
            .AddIf(PhotoTag.Panorama, IsPanorama(metadata))
            .AddIf(PhotoTag.Raw, IsRaw(filePath))
            .AddIf(PhotoTag.LivePhoto, IsLivePhoto(filePath))
            ;

        _logger.LogInformation("Photo tags generated: {PhotoTags}", tags);

        return Task.FromResult<IReadOnlySet<PhotoTag>>(tags);
    }

    private bool IsScreenshot(IReadOnlyList<Directory> metadata)
    {
        var userComments = metadata
            .OfType<ExifDirectoryBase>()
            .Select(directory => directory.GetDescription(ExifDirectoryBase.TagUserComment));

        return userComments.Any(value => value == UserCommentValueScreenshot);
    }

    private bool IsVideo(string filePath)
    {
        var fileExtension = Path.GetExtension(filePath);

        return KnownFileExtensions.VideoExtensions.Contains(fileExtension);
    }

    private bool IsMotionPhoto(IReadOnlyList<Directory> metadata)
    {
        var motionPhotoProperties = metadata
            .OfType<XmpDirectory>()
            .Where(d => d.XmpMeta?.DoesPropertyExist(GoogleCamera, MotionPhotoPropertyName) == true)
            .Select(d =>
            {
                try
                {
                    return d.XmpMeta?.GetPropertyInteger(GoogleCamera, MotionPhotoPropertyName);
                }
                catch (XmpException ex)
                {
                    _logger.LogError("Photo tag generation failed (MotionPhoto): {ErrorMessage}", ex.CombinedMessage());
                    return null;
                }
            });

        return motionPhotoProperties.Any(value => value == 1);
    }

    private bool IsSelfie(IReadOnlyList<Directory> metadata)
    {
        var lensModels = metadata
            .OfType<ExifDirectoryBase>()
            .Select(directory => directory.GetString(ExifDirectoryBase.TagLensModel));

        return lensModels.Any(value => value?.Contains(LensModelValueFragmentFront) == true);
    }

    private bool IsPortrait(IReadOnlyList<Directory> metadata)
    {
        var customRenderedProperties = metadata
            .OfType<ExifDirectoryBase>()
            .Select(directory => directory.TryGetInt32(ExifDirectoryBase.TagCustomRendered, out var customRendered) ? customRendered : 0);

        return customRenderedProperties.Any(value => value == 7);
    }

    private bool IsPanorama(IReadOnlyList<Directory> metadata)
    {
        var projectionTypes = metadata
            .OfType<XmpDirectory>()
            .Where(d => d.XmpMeta?.DoesPropertyExist(GooglePanorama, ProjectionTypePropertyName) == true)
            .Select(d =>
            {
                try
                {
                    return d.XmpMeta?.GetPropertyString(GooglePanorama, ProjectionTypePropertyName);
                }
                catch (XmpException ex)
                {
                    _logger.LogError("Photo tag generation failed (Panorama): {ErrorMessage}", ex.CombinedMessage());
                    return null;
                }
            });

        return projectionTypes.Any(value => value == ProjectionTypeValueEquirectangular);
    }

    private bool IsRaw(string filePath)
    {
        var fileExtension = Path.GetExtension(filePath);

        return KnownFileExtensions.RawImageExtensions.Contains(fileExtension);
    }

    private bool IsLivePhoto(string filePath)
    {
        return _livePhotoFileDetector.IsLivePhoto(filePath);
    }

    private IReadOnlyList<Directory> ReadMetadata(string filePath)
    {
        try
        {
            return ImageMetadataReader.ReadMetadata(filePath);
        }
        catch (Exception ex)
        {
            LogException(ex);

            return [];
        }

        void LogException(Exception exception)
        {
            if (exception.IsFileAccessException())
            {
                _logger.LogWarning(
                    "Photo tags generation failed: {ErrorCode} {ErrorMessage}",
                    exception.GetRelevantFormattedErrorCode(),
                    exception.Message);
            }
            else
            {
                _logger.LogWarning(
                    "Photo tags generation failed: {ExceptionType}: {ErrorMessage}",
                    exception.GetType().Name,
                    exception.Message);
            }
        }
    }
}
