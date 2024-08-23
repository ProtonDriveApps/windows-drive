using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using ProtonDrive.App.SystemIntegration;

namespace ProtonDrive.App.Windows.SystemIntegration;

internal sealed class NtfsPermissionsBasedSyncFolderStructureProtector : ISyncFolderStructureProtector
{
    private static readonly SecurityIdentifier EveryoneUser = new(WellKnownSidType.WorldSid, null);
    private static readonly Dictionary<FolderProtectionType, FileSystemRights[]> FolderRights = new()
    {
        {
            FolderProtectionType.Ancestor,
            [
                FileSystemRights.DeleteSubdirectoriesAndFiles,
                FileSystemRights.CreateDirectories,
                FileSystemRights.CreateFiles,
            ]
        },
        {
            FolderProtectionType.AncestorWithFiles,
            [
                FileSystemRights.DeleteSubdirectoriesAndFiles,
                FileSystemRights.CreateDirectories,
            ]
        },
        {
            FolderProtectionType.Leaf,
            [
                FileSystemRights.Delete,
            ]
        },
    };

    public bool Protect(string folderPath, FolderProtectionType protectionType)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException("The folder to protect does not exist");
        }

        AddDirectorySecurity(folderPath, FolderRights[protectionType], AccessControlType.Deny);

        return true;
    }

    public bool Unprotect(string folderPath, FolderProtectionType protectionType)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException("The folder to unprotect does not exist");
        }

        RemoveDirectorySecurity(folderPath, FolderRights[protectionType], AccessControlType.Deny);

        return true;
    }

    private static void AddDirectorySecurity(string path, IEnumerable<FileSystemRights> rights, AccessControlType controlType)
    {
        var directoryInfo = new DirectoryInfo(path);

        var directorySecurity = GetAccessControl(directoryInfo);

        foreach (var right in rights)
        {
            directorySecurity.AddAccessRule(new FileSystemAccessRule(
                EveryoneUser,
                right,
                InheritanceFlags.None,
                PropagationFlags.NoPropagateInherit,
                controlType));
        }

        SetAccessControl(directoryInfo, directorySecurity);
    }

    private static void RemoveDirectorySecurity(string path, IEnumerable<FileSystemRights> rights, AccessControlType controlType)
    {
        var directoryInfo = new DirectoryInfo(path);
        var directorySecurity = GetAccessControl(directoryInfo);

        foreach (var right in rights)
        {
            directorySecurity.RemoveAccessRule(new FileSystemAccessRule(EveryoneUser, right, controlType));
        }

        SetAccessControl(directoryInfo, directorySecurity);
    }

    private static DirectorySecurity GetAccessControl(DirectoryInfo directoryInfo)
    {
        try
        {
            return directoryInfo.GetAccessControl();
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException("Unable to get access control", ex);
        }
    }

    private static void SetAccessControl(DirectoryInfo directoryInfo, DirectorySecurity directorySecurity)
    {
        try
        {
            directoryInfo.SetAccessControl(directorySecurity);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException("Unable to set folder access control", ex);
        }
    }
}
