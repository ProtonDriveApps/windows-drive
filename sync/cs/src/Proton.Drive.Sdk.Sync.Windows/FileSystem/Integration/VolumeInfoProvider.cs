using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Sdk.Sync.Windows.Interop;
using Proton.Drive.Shared;
using Proton.Drive.Shared.IO;
using Proton.Drive.Shared.Logging;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Integration;

public class VolumeInfoProvider : ILocalVolumeInfoProvider
{
    private const int PathMaxLength = 260;
    private const string NtfsVolumeName = "NTFS";
    private const string ReFsVolumeName = "ReFS";

    private readonly ILogger<VolumeInfoProvider> _logger;

    public VolumeInfoProvider(ILogger<VolumeInfoProvider> logger)
    {
        _logger = logger;
    }

    public bool IsNtfsFileSystem(string path)
    {
        if (!TryGetFileSystemName(path, out var fileSystemName))
        {
            return false;
        }

        if (!fileSystemName.Equals(NtfsVolumeName, StringComparison.OrdinalIgnoreCase))
        {
            LogUnsupportedFileSystem(path, fileSystemName);

            return false;
        }

        return true;
    }

    public bool IsClassicSyncSupportedFileSystem(string path)
    {
        if (!TryGetFileSystemName(path, out var fileSystemName))
        {
            return false;
        }

        if (fileSystemName.Equals(NtfsVolumeName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (fileSystemName.Equals(ReFsVolumeName, StringComparison.OrdinalIgnoreCase))
        {
            return HasSupportedReFsFileIds(path);
        }

        LogUnsupportedFileSystem(path, fileSystemName);

        return false;
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
        try
        {
            rootPath = Internal.FileSystem.GetVolumeRoot(path);
            rootPath = PathComparison.EnsureTrailingSeparator(rootPath);

            return true;
        }
        catch (Win32Exception exception)
        {
            var pathToLog = _logger.GetSensitiveValueForLogging(path);
            _logger.LogWarning(
                "Failed to get local volume root path for path \"{Path}\", Win32 error {ErrorCode}: {ErrorMessage}",
                pathToLog,
                exception.NativeErrorCode,
                exception.Message);

            rootPath = null;
            return false;
        }
    }

    private bool TryGetFileSystemName(string path, [NotNullWhen(true)] out string? fileSystemName)
    {
        if (!TryGetVolumeRoot(path, out var volumeRootPath))
        {
            fileSystemName = null;
            return false;
        }

        return TryGetVolumeFileSystemName(volumeRootPath, out fileSystemName);
    }

    private void LogUnsupportedFileSystem(string path, string fileSystemName)
    {
        var pathToLog = _logger.GetSensitiveValueForLogging(path);
        _logger.LogWarning(
            "File system name for path \"{Path}\" is \"{FileSystemName}\"",
            pathToLog,
            fileSystemName);
    }

    /// <summary>
    /// Checks that ReFS packs the 128-bit file ID of the given folder into its 64-bit file ID
    /// the way <see cref="ReFsFileId"/> expects. The sync engine identifies files by the 64-bit ID,
    /// which ReFS does not guarantee to be unique in general.
    /// </summary>
    private bool HasSupportedReFsFileIds(string path)
    {
        var pathToLog = _logger.GetSensitiveValueForLogging(path);

        try
        {
            using var handle = Internal.FileSystem.CreateHandle(
                path,
                FileMode.Open,
                FileSystemFileAccess.ReadAttributes,
                FileShare.ReadWrite | FileShare.Delete,
                attributes: default,
                (FileOptions)Kernel32.FileFlags.FILE_FLAG_BACKUP_SEMANTICS);

            var information = Internal.FileSystem.GetFileInformation(handle);
            var idInformation = Internal.FileSystem.GetFileIdInfo(handle);
            var fileId = unchecked((long)(((ulong)information.nFileIndexHigh << 32) | information.nFileIndexLow));

            if (ReFsFileId.IsLosslessRepresentation(fileId, idInformation.FileIdHigh, idInformation.FileIdLow))
            {
                _logger.LogInformation("ReFS file ID scheme for path \"{Path}\" is supported", pathToLog);

                return true;
            }

            _logger.LogWarning(
                "ReFS file ID scheme for path \"{Path}\" is not supported: 64-bit ID 0x{FileId:X16}, 128-bit ID 0x{FileIdHigh:X16}{FileIdLow:X16}",
                pathToLog,
                fileId,
                idInformation.FileIdHigh,
                idInformation.FileIdLow);

            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception)
        {
            _logger.LogWarning(
                "Failed to check ReFS file ID scheme for path \"{Path}\": {ErrorMessage}",
                pathToLog,
                exception.Message);

            return false;
        }
    }

    private bool TryGetVolumeFileSystemName(string volumeRootPath, [NotNullWhen(true)] out string? fileSystemName)
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
