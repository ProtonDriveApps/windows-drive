namespace ProtonDrive.App.SystemIntegration;

public interface ISyncFolderStructureProtector
{
    public bool Protect(string folderPath, FolderProtectionType protectionType);
    public bool Unprotect(string folderPath, FolderProtectionType protectionType);
}
