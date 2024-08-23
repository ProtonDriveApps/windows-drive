using System;
using System.IO;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.App.Windows.SystemIntegration;

internal sealed class SafeSyncFolderStructureProtectorDecorator : ISyncFolderStructureProtector
{
    private readonly ISyncFolderStructureProtector _decoratedInstance;

    public SafeSyncFolderStructureProtectorDecorator(ISyncFolderStructureProtector decoratedInstance)
    {
        _decoratedInstance = decoratedInstance;
    }

    public bool Protect(string folderPath, FolderProtectionType protectionType)
    {
        try
        {
            return _decoratedInstance.Protect(folderPath, protectionType);
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is SystemException)
        {
            return false;
        }
    }

    public bool Unprotect(string folderPath, FolderProtectionType protectionType)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            return true;
        }

        try
        {
            return _decoratedInstance.Unprotect(folderPath, protectionType);
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is SystemException)
        {
            return false;
        }
    }
}
