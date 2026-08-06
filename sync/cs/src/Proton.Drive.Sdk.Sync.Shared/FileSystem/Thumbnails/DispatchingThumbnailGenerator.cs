using Microsoft.Extensions.Logging;

namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Thumbnails;

public sealed class DispatchingThumbnailGenerator : IThumbnailGenerator
{
    private readonly IEnumerable<IThumbnailGenerator> _thumbnailGenerators;
    private readonly ILogger<IThumbnailGenerator> _logger;

    public DispatchingThumbnailGenerator(IEnumerable<IThumbnailGenerator> thumbnailGenerators, ILogger<IThumbnailGenerator> logger)
    {
        _thumbnailGenerators = thumbnailGenerators;
        _logger = logger;
    }

    public async Task<ReadOnlyMemory<byte>?> TryGenerateThumbnailAsync(
        string filePath,
        int numberOfPixelsOnLargestSide,
        int maxNumberOfBytes,
        CancellationToken cancellationToken)
    {
        var errorCode = ThumbnailGenerationErrorCode.None;

        foreach (var thumbnailGenerator in _thumbnailGenerators)
        {
            try
            {
                return await thumbnailGenerator
                    .TryGenerateThumbnailAsync(filePath, numberOfPixelsOnLargestSide, maxNumberOfBytes, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ThumbnailGenerationException ex)
            {
                if ((int)errorCode < (int)ex.ErrorCode)
                {
                    errorCode = ex.ErrorCode;
                }
            }
        }

        if (errorCode == ThumbnailGenerationErrorCode.ExtensionNotSupported)
        {
            var extension = Path.GetExtension(filePath);
            var fileExtensionToLog = extension[^Math.Min(extension.Length, 5)..].ToLowerInvariant();

            _logger.LogInformation(
                "Thumbnail generation skipped: File extension \"{Extension}\" not supported",
                fileExtensionToLog);
        }

        return null;
    }
}
