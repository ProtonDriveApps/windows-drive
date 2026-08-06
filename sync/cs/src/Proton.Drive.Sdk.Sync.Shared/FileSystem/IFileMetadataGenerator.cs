namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

public interface IFileMetadataGenerator
{
    Task<FileMetadata?> GetMetadataAsync(string filePath);
}
