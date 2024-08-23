using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Mapping.Setup;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Sync.Windows.FileSystem;
using ProtonDrive.Sync.Windows.FileSystem.CloudFiles;
using static Vanara.PInvoke.CldApi;

namespace ProtonDrive.App.Windows.SystemIntegration;

internal sealed class LocalFolderService : ILocalFolderService
{
    private const string SearchAll = "*";

    private readonly ILogger<LocalFolderService> _logger;

    public LocalFolderService(ILogger<LocalFolderService> logger)
    {
        _logger = logger;
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public bool FolderExists(string? path)
    {
        return Directory.Exists(path);
    }

    public bool NonEmptyFolderExists(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return SafeNonEmptyFolderExists(path);
    }

    public bool EmptyFolderExists(string? path, ISet<string>? subfoldersToIgnore = null)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var enumerationOptions = new EnumerationOptions
        {
            AttributesToSkip = FileAttributes.System,
            IgnoreInaccessible = true,
        };

        try
        {
            return Directory.Exists(path) &&
                   !Directory.EnumerateFiles(path, SearchAll, enumerationOptions).Any() &&
                   Directory.EnumerateDirectories(path, SearchAll, enumerationOptions).All(f => subfoldersToIgnore?.Contains(f) == true);
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            return false;
        }
    }

    public bool TryGetFolderInfo(string path, FileShare shareMode, out LocalFolderInfo? folderInfo)
    {
        folderInfo = default;

        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        try
        {
            using var folder = FileSystemObject.Open(path, FileMode.Open, FileSystemFileAccess.ReadData, shareMode, FileOptions.None);

            if (!folder.Attributes.HasFlag(FileAttributes.Directory))
            {
                // Folder on the path does not exist, file exists
                var pathToLog = _logger.GetSensitiveValueForLogging(path);
                _logger.LogWarning("Failed to get local folder information for \"{Path}\", file found", pathToLog);

                return true;
            }

            if (!TryGetVolumeInfo(folder, out var volumeInfo))
            {
                return false;
            }

            folderInfo = new LocalFolderInfo
            {
                Id = folder.ObjectId,
                VolumeInfo = volumeInfo,
            };

            return true;
        }
        catch (FileNotFoundException)
        {
            // Folder on the path does not exist
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            // Folder on the path does not exist
            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is Win32Exception)
        {
            var pathToLog = _logger.GetSensitiveValueForLogging(path);
            _logger.LogWarning("Failed to get local folder information for \"{Path}\": {ErrorMessage}", pathToLog, ex.Message);

            return false;
        }
    }

    public Task<bool> OpenFolderAsync(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Task.FromResult(false);
        }

        return Task.Run(() => SafeOpenFolder(path));
    }

    public string? GetDefaultAccountRootFolderPath(string userDataPath, string? username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return default;
        }

        var folderNameGenerator = new NumberSuffixedNameGenerator(username, NameType.Folder);

        foreach (var folderNameCandidate in folderNameGenerator.GenerateNames())
        {
            var path = Path.Combine(userDataPath, folderNameCandidate);

            if (!NonEmptyFolderExists(path))
            {
                return path;
            }
        }

        _logger.LogWarning("Failed to generate a unique account root folder path");
        return default;
    }

    public bool TryConvertToPlaceholder(string path)
    {
        try
        {
            using var fileSystemDirectory = FileSystemDirectory.Open(path, FileSystemFileAccess.WriteAttributes);

            if (!fileSystemDirectory.GetPlaceholderState().HasFlag(PlaceholderState.Placeholder))
            {
                fileSystemDirectory.ConvertToPlaceholder(default, CF_CONVERT_FLAGS.CF_CONVERT_FLAG_MARK_IN_SYNC);
            }

            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is COMException)
        {
            var pathToLog = _logger.GetSensitiveValueForLogging(path);

            _logger.LogWarning(
                "Failed to convert local folder \"{Path}\" to placeholder: {ExceptionType}: {ErrorCode}",
                pathToLog,
                ex.GetType().Name,
                ex.GetRelevantFormattedErrorCode());

            return false;
        }
    }

    private bool TryGetVolumeInfo(FileSystemObject fileSystemObject, [NotNullWhen(true)] out LocalVolumeInfo? volumeInfo)
    {
        try
        {
            volumeInfo = fileSystemObject.GetVolumeInfo();

            return true;
        }
        catch (Win32Exception ex)
        {
            var pathToLog = _logger.GetSensitiveValueForLogging(fileSystemObject.FullPath);
            _logger.LogWarning(
                "Failed to get local volume information for path \"{Path}\", Win32 error {ErrorCode}: {ErrorMessage}",
                pathToLog,
                ex.NativeErrorCode,
                ex.Message);

            volumeInfo = null;
            return false;
        }
    }

    private bool SafeNonEmptyFolderExists(string path)
    {
        var enumerationOptions = new EnumerationOptions
        {
            AttributesToSkip = FileAttributes.System,
            IgnoreInaccessible = true,
        };

        try
        {
            return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path, SearchAll, enumerationOptions).Any();
        }
        catch (Exception ex) when (ex is IOException or Win32Exception or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool SafeOpenFolder(string path)
    {
        if (!FolderExists(path))
        {
            return false;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    Verb = "explore",
                });
        }
        catch (Exception ex) when (ex is IOException or Win32Exception or UnauthorizedAccessException)
        {
            return false;
        }

        return true;
    }
}
