namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Metadata.GoogleTakeout;

public sealed class GoogleTakeoutMetadataExtractingDecorator : IFileMetadataGenerator
{
    private readonly IFileMetadataGenerator _decoratedInstance;
    private readonly IGoogleTakeoutMetadataExtractor _metadataExtractor;

    public GoogleTakeoutMetadataExtractingDecorator(IFileMetadataGenerator decoratedInstance, IGoogleTakeoutMetadataExtractor metadataExtractor)
    {
        _decoratedInstance = decoratedInstance;
        _metadataExtractor = metadataExtractor;
    }

    public async Task<FileMetadata?> GetMetadataAsync(string filePath)
    {
        var generatedMetadata = await _decoratedInstance.GetMetadataAsync(filePath).ConfigureAwait(false);

        var takeoutMetadata = _metadataExtractor.ExtractMetadata(filePath);

        if (takeoutMetadata is null)
        {
            return generatedMetadata;
        }

        return (generatedMetadata ?? FileMetadata.Empty) with
        {
            CaptureTime = takeoutMetadata.CaptureTime ?? generatedMetadata?.CaptureTime,
            Latitude = takeoutMetadata.Latitude ?? generatedMetadata?.Latitude,
            Longitude = takeoutMetadata.Longitude ?? generatedMetadata?.Longitude,
        };
    }
}
