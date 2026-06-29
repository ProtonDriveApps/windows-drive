namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;

public interface IReadOnlyFileAttributeRemover
{
    bool TryRemoveFileReadOnlyAttributeInFolder(string folderPath);
    bool TryRemoveFileReadOnlyAttribute(string filePath);
}
