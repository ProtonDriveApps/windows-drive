namespace ProtonDrive.Sync.Shared.FileSystem.Metadata.GoogleTakeout;

public interface IGoogleTakeoutMetadataExtractor
{
    GoogleTakeoutMetadata? ExtractMetadata(string filePath);
}
