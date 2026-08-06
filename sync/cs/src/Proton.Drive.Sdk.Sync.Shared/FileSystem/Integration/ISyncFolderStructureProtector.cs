namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;

public interface ISyncFolderStructureProtector
{
    bool ProtectFolder(string folderPath, FolderProtectionType protectionType);
    bool UnprotectFolder(string folderPath, FolderProtectionType protectionType);
    bool ProtectFile(string filePath, FileProtectionType protectionType);
    bool UnprotectFile(string filerPath, FileProtectionType protectionType);
    bool UnprotectBranch(string folderPath, FolderProtectionType folderProtectionType, FileProtectionType fileProtectionType);
}
