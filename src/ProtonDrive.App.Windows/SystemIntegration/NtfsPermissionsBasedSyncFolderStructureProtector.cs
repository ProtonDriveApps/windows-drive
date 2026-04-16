using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Sync.Windows.FileSystem;

namespace ProtonDrive.App.Windows.SystemIntegration;

internal sealed class NtfsPermissionsBasedSyncFolderStructureProtector : ISyncFolderStructureProtector
{
    private static readonly TimeSpan ActionDurationWarningThreshold = TimeSpan.FromSeconds(2);

    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        RecurseSubdirectories = false,
        AttributesToSkip = FileAttributes.None, // By default, Hidden and System attributes are skipped
    };

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
        {
            FolderProtectionType.ReadOnly,
            [
                FileSystemRights.Delete,
                FileSystemRights.DeleteSubdirectoriesAndFiles,
                FileSystemRights.CreateDirectories,
                FileSystemRights.CreateFiles,
            ]
        },
    };

    private static readonly Dictionary<FileProtectionType, FileSystemRights[]> FileRights = new()
    {
        {
            FileProtectionType.ReadOnly,
            [
                FileSystemRights.WriteData,
                FileSystemRights.AppendData,
                FileSystemRights.Delete,
            ]
        },
    };

    private readonly ILogger<NtfsPermissionsBasedSyncFolderStructureProtector> _logger;

    public NtfsPermissionsBasedSyncFolderStructureProtector(ILogger<NtfsPermissionsBasedSyncFolderStructureProtector> logger)
    {
        _logger = logger;
    }

    public bool ProtectFolder(string folderPath, FolderProtectionType protectionType)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException("The folder to protect does not exist");
        }

        AddDirectorySecurity(folderPath, FolderRights[protectionType], AccessControlType.Deny);

        return true;
    }

    public bool UnprotectFolder(string folderPath, FolderProtectionType protectionType)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException("The folder to unprotect does not exist");
        }

        RemoveDirectorySecurity(folderPath, FolderRights[protectionType], AccessControlType.Deny);

        return true;
    }

    public bool ProtectFile(string filePath, FileProtectionType protectionType)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The file to protect does not exist");
        }

        AddFileSecurity(filePath, FileRights[protectionType], AccessControlType.Deny);

        return true;
    }

    public bool UnprotectFile(string filePath, FileProtectionType protectionType)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The file to unprotect does not exist");
        }

        RemoveFileSecurity(filePath, FileRights[protectionType], AccessControlType.Deny);

        return true;
    }

    public bool UnprotectBranch(string folderPath, FolderProtectionType folderProtectionType, FileProtectionType fileProtectionType)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException("The branch to unprotect does not exist");
        }

        RemoveBranchProtection(folderPath, folderProtectionType, fileProtectionType);

        return true;
    }

    private void RemoveBranchProtection(string folderPath, FolderProtectionType folderProtectionType, FileProtectionType fileProtectionType)
    {
        using var folder = FileSystemDirectory.Open(folderPath, FileSystemFileAccess.Read);

        var entries = folder.EnumerateFileSystemEntries(options: EnumerationOptions);

        foreach (var entry in entries)
        {
            var entryFullPath = Path.Combine(folder.FullPath, entry.Name);

            if (entry.Attributes.HasFlag(FileAttributes.Directory))
            {
                RemoveBranchProtection(entryFullPath, folderProtectionType, fileProtectionType);
            }
            else
            {
                RemoveFileSecurity(entryFullPath, FileRights[fileProtectionType], AccessControlType.Deny);
            }
        }

        RemoveDirectorySecurity(folderPath, FolderRights[folderProtectionType], AccessControlType.Deny);
    }

    private void AddDirectorySecurity(string path, IEnumerable<FileSystemRights> rights, AccessControlType controlType)
    {
        var directoryInfo = new DirectoryInfo(path);

        var directorySecurity = GetAccessControl(directoryInfo);

        var existingRules = directorySecurity
            .GetAccessRules(includeExplicit: true, includeInherited: false, targetType: typeof(SecurityIdentifier));

        var modified = false;

        foreach (var right in rights)
        {
            var alreadyExists = existingRules
                .OfType<FileSystemAccessRule>()
                .Any(r => r.IdentityReference.Equals(EveryoneUser)
                        && r.FileSystemRights.HasFlag(right)
                        && r.AccessControlType == controlType
                        && r.InheritanceFlags == InheritanceFlags.None);

            if (alreadyExists)
            {
                continue;
            }

            directorySecurity.AddAccessRule(new FileSystemAccessRule(
                EveryoneUser,
                right,
                InheritanceFlags.None,
                PropagationFlags.None,
                controlType));

            modified = true;
        }

        if (modified)
        {
            SetAccessControl(directoryInfo, directorySecurity);
        }
    }

    private void RemoveDirectorySecurity(string path, IEnumerable<FileSystemRights> rights, AccessControlType controlType)
    {
        var directoryInfo = new DirectoryInfo(path);
        var directorySecurity = GetAccessControl(directoryInfo);

        foreach (var right in rights)
        {
            directorySecurity.RemoveAccessRule(new FileSystemAccessRule(EveryoneUser, right, controlType));
        }

        SetAccessControl(directoryInfo, directorySecurity);
    }

    private void AddFileSecurity(string path, IEnumerable<FileSystemRights> rights, AccessControlType controlType)
    {
        var fileInfo = new FileInfo(path);

        var fileSecurity = GetAccessControl(fileInfo);

        var existingRules = fileSecurity
            .GetAccessRules(includeExplicit: true, includeInherited: false, targetType: typeof(SecurityIdentifier));

        var modified = false;

        foreach (var right in rights)
        {
            var alreadyExists = existingRules
                .OfType<FileSystemAccessRule>()
                .Any(r => r.IdentityReference.Equals(EveryoneUser)
                        && r.FileSystemRights.HasFlag(right)
                        && r.AccessControlType == controlType
                        && r.InheritanceFlags == InheritanceFlags.None);

            if (alreadyExists)
            {
                continue;
            }

            fileSecurity.AddAccessRule(new FileSystemAccessRule(
                EveryoneUser,
                right,
                InheritanceFlags.None,
                PropagationFlags.None,
                controlType));

            modified = true;
        }

        if (modified)
        {
            SetAccessControl(fileInfo, fileSecurity);
        }
    }

    private void RemoveFileSecurity(string path, IEnumerable<FileSystemRights> rights, AccessControlType controlType)
    {
        var fileInfo = new FileInfo(path);
        var fileSecurity = GetAccessControl(fileInfo);

        foreach (var right in rights)
        {
            fileSecurity.RemoveAccessRule(new FileSystemAccessRule(EveryoneUser, right, controlType));
        }

        SetAccessControl(fileInfo, fileSecurity);
    }

    private DirectorySecurity GetAccessControl(DirectoryInfo directoryInfo)
    {
        try
        {
            var startTimestamp = LogStartAndGetTimestamp("Obtaining", directoryInfo.FullName);

            var result = directoryInfo.GetAccessControl();

            LogSuccess("Obtained", startTimestamp, directoryInfo.FullName);

            return result;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException("Unable to get folder access control", ex);
        }
    }

    private void SetAccessControl(DirectoryInfo directoryInfo, DirectorySecurity directorySecurity)
    {
        try
        {
            var startTimestamp = LogStartAndGetTimestamp("Updating", directoryInfo.FullName);

            directoryInfo.SetAccessControl(directorySecurity);

            LogSuccess("Updated", startTimestamp, directoryInfo.FullName);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException("Unable to set folder access control", ex);
        }
    }

    private FileSecurity GetAccessControl(FileInfo fileInfo)
    {
        try
        {
            return fileInfo.GetAccessControl();
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException("Unable to get file access control", ex);
        }
    }

    private void SetAccessControl(FileInfo fileInfo, FileSecurity fileSecurity)
    {
        try
        {
            fileInfo.SetAccessControl(fileSecurity);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException("Unable to set file access control", ex);
        }
    }

    private long LogStartAndGetTimestamp(string action, string path)
    {
        _logger.LogDebug("{Action} folder \"{Path}\" protection", action, path);
        return Stopwatch.GetTimestamp();
    }

    private void LogSuccess(string action, long startTimestamp, string path)
    {
        var elapsed = Stopwatch.GetElapsedTime(startTimestamp);

        if (elapsed < ActionDurationWarningThreshold)
        {
            _logger.LogDebug("{Action} folder \"{Path}\" protection in {ElapsedTime}", action, path, elapsed);
        }
        else
        {
            var pathForLogging = _logger.GetSensitiveValueForLogging(path);
            _logger.LogWarning("{Action} folder \"{Path}\" protection in {ElapsedTime}", action, pathForLogging, elapsed);
        }
    }
}
