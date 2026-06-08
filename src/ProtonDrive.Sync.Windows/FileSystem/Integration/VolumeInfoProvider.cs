using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared;
using ProtonDrive.Shared.IO;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Sync.Shared.FileSystem.Integration;
using ProtonDrive.Sync.Windows.Interop;

namespace ProtonDrive.Sync.Windows.FileSystem.Integration;

public class VolumeInfoProvider : ILocalVolumeInfoProvider
{
    private const int PathMaxLength = 260;
    private const string NtfsVolumeName = "NTFS";

    private readonly ILogger<VolumeInfoProvider> _logger;

    public VolumeInfoProvider(ILogger<VolumeInfoProvider> logger)
    {
        _logger = logger;
    }

    public bool IsNtfsFileSystem(string path)
    {
        if (!TryGetVolumeRoot(path, out var volumeRootPath))
        {
            return false;
        }

        if (!TryGetFileSystemName(volumeRootPath, out var fileSystemName))
        {
            return false;
        }

        if (!fileSystemName.Equals(NtfsVolumeName, StringComparison.OrdinalIgnoreCase))
        {
            var pathToLog = _logger.GetSensitiveValueForLogging(path);
            _logger.LogWarning(
                "File system name for path \"{Path}\" is \"{FileSystemName}\"",
                pathToLog,
                fileSystemName);

            return false;
        }

        return true;
    }

    public long? GetAvailableFreeSpace(string path)
    {
        Ensure.NotNullOrEmpty(path, nameof(path));

        if (!TryGetDiskFreeSpace(path, out var freeBytesAvailable))
        {
            return null;
        }

        // Returns maximum value in case of cast overflow
        var result = unchecked((long)freeBytesAvailable);
        return result >= 0 ? result : long.MaxValue;
    }

    public DriveType GetDriveType(string path)
    {
        if (!TryGetVolumeRoot(path, out var volumeRootPath))
        {
            return DriveType.Unknown;
        }

        if (Shlwapi.PathIsNetworkPath(volumeRootPath))
        {
            return DriveType.Network;
        }

        return Kernel32.GetDriveType(volumeRootPath);
    }

    private bool TryGetDiskFreeSpace(string path, out ulong freeBytesAvailable)
    {
        if (!Path.EndsInDirectorySeparator(path))
        {
            path += Path.DirectorySeparatorChar;
        }

        if (!Kernel32.GetDiskFreeSpaceEx(path, out freeBytesAvailable, out _, out _))
        {
            var exception = new Win32Exception();   // Automatically gets the last Win32 error code and description
            var pathToLog = _logger.GetSensitiveValueForLogging(path);
            _logger.LogWarning(
                "Failed to get local disk free space for path \"{path}\", Win32 error {ErrorCode}: {ErrorMessage}",
                pathToLog,
                exception.NativeErrorCode,
                exception.Message);

            return false;
        }

        return true;
    }

    private bool TryGetVolumeRoot(string path, [NotNullWhen(true)] out string? rootPath)
    {
        var rootPathBuffer = new StringBuilder(PathMaxLength);

        if (!Kernel32.GetVolumePathName(path, rootPathBuffer, (uint)rootPathBuffer.Capacity))
        {
            var exception = new Win32Exception();   // Automatically gets the last Win32 error code and description
            var pathToLog = _logger.GetSensitiveValueForLogging(path);
            _logger.LogWarning(
                "Failed to get local volume root path for path \"{Path}\", Win32 error {ErrorCode}: {ErrorMessage}",
                pathToLog,
                exception.NativeErrorCode,
                exception.Message);

            rootPath = null;
            return false;
        }

        rootPath = PathComparison.EnsureTrailingSeparator(rootPathBuffer.ToString());
        return true;
    }

    private bool TryGetFileSystemName(string volumeRootPath, [NotNullWhen(true)] out string? fileSystemName)
    {
        var volumeNameBuffer = new StringBuilder(PathMaxLength);
        var fileSystemNameBuffer = new StringBuilder(PathMaxLength);

        if (!Kernel32.GetVolumeInformation(
                volumeRootPath,
                volumeNameBuffer,
                volumeNameBuffer.Capacity,
                out _,
                out _,
                out _,
                fileSystemNameBuffer,
                fileSystemNameBuffer.Capacity))
        {
            var exception = new Win32Exception();   // Automatically gets the last Win32 error code and description
            _logger.LogWarning(
                "Failed to get local file system name for volume \"{Volume}\", Win32 error {ErrorCode}: {ErrorMessage}",
                volumeRootPath,
                exception.NativeErrorCode,
                exception.Message);

            fileSystemName = null;
            return false;
        }

        fileSystemName = fileSystemNameBuffer.ToString();
        return true;
    }
}
