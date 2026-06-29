using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Integration;

public sealed class SafeSyncFolderStructureProtectorDecorator : ISyncFolderStructureProtector
{
    private readonly ISyncFolderStructureProtector _decoratedInstance;

    public SafeSyncFolderStructureProtectorDecorator(ISyncFolderStructureProtector decoratedInstance)
    {
        _decoratedInstance = decoratedInstance;
    }

    public bool ProtectFolder(string folderPath, FolderProtectionType protectionType)
    {
        try
        {
            return _decoratedInstance.ProtectFolder(folderPath, protectionType);
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is SystemException)
        {
            return false;
        }
    }

    public bool UnprotectFolder(string folderPath, FolderProtectionType protectionType)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            return true;
        }

        try
        {
            return _decoratedInstance.UnprotectFolder(folderPath, protectionType);
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is SystemException)
        {
            return false;
        }
    }

    public bool ProtectFile(string filePath, FileProtectionType protectionType)
    {
        try
        {
            return _decoratedInstance.ProtectFile(filePath, protectionType);
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is SystemException)
        {
            return false;
        }
    }

    public bool UnprotectFile(string filePath, FileProtectionType protectionType)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return true;
        }

        try
        {
            return _decoratedInstance.UnprotectFile(filePath, protectionType);
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is SystemException)
        {
            return false;
        }
    }

    public bool UnprotectBranch(string folderPath, FolderProtectionType folderProtectionType, FileProtectionType fileProtectionType)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            return true;
        }

        try
        {
            return _decoratedInstance.UnprotectBranch(folderPath, folderProtectionType, fileProtectionType);
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is SystemException)
        {
            return false;
        }
    }
}
