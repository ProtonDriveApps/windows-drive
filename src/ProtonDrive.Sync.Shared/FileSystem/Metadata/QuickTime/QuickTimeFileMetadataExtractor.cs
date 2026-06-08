using System.Collections.Frozen;
using MetadataExtractor;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using Directory = MetadataExtractor.Directory;

namespace ProtonDrive.Sync.Shared.FileSystem.Metadata.QuickTime;

public sealed class QuickTimeFileMetadataExtractor
{
    private static readonly FrozenSet<string> SupportedQuickTimeVideoFileExtensions = new HashSet<string>
    {
        ".mp4", // MPEG-4 Part 14 (most common)
        ".mov", // QuickTime Movie
        ".m4v", // iTunes Video
        ".3gp", // 3GPP (mobile)
        ".3g2", // 3GPP2 (mobile)
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly ILogger<QuickTimeFileMetadataExtractor> _logger;

    public QuickTimeFileMetadataExtractor(ILogger<QuickTimeFileMetadataExtractor> logger)
    {
        _logger = logger;
    }

    public FileMetadata? GetMetadata(string filePath)
    {
        var extension = Path.GetExtension(filePath);

        if (!SupportedQuickTimeVideoFileExtensions.Contains(extension))
        {
            _logger.LogDebug("Not a QuickTime video file");
            return null;
        }

        _logger.LogDebug("Reading QuickTime metadata");

        var metadata = ReadMetadata(filePath);

        if (!metadata.Any())
        {
            return null;
        }

        var quickTimeMetadata = metadata.ExtractQuickTimeMetadata();

        if (quickTimeMetadata is null)
        {
            _logger.LogWarning("No QuickTime metadata extracted");
        }
        else
        {
            _logger.LogInformation("QuickTime metadata extracted");
        }

        return quickTimeMetadata;
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
                    "Reading QuickTime metadata failed: {ErrorCode} {ErrorMessage}",
                    exception.GetRelevantFormattedErrorCode(),
                    exception.Message);
            }
            else
            {
                _logger.LogWarning(
                    "Reading QuickTime metadata failed: {ExceptionType}: {ErrorMessage}",
                    exception.GetType().Name,
                    exception.Message);
            }
        }
    }
}
