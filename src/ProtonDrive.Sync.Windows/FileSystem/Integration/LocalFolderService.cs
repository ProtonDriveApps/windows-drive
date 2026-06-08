using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Sync.Shared.FileSystem.Integration;
using ProtonDrive.Sync.Windows.FileSystem.OnDemand;
using static Vanara.PInvoke.CldApi;

namespace ProtonDrive.Sync.Windows.FileSystem.Integration;

internal sealed class LocalFolderService : ILocalFolderService
{
    private const string SearchAll = "*";

    private readonly INumberSuffixedNameGenerator _numberSuffixedNameGenerator;
    private readonly ILogger<LocalFolderService> _logger;

    public LocalFolderService(
        INumberSuffixedNameGenerator numberSuffixedNameGenerator,
        ILogger<LocalFolderService> logger)
    {
        _numberSuffixedNameGenerator = numberSuffixedNameGenerator;
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
        folderInfo = null;

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
            _logger.LogWarning(
                "Failed to get local folder information for \"{Path}\": {ExceptionType} {ErrorCode}",
                pathToLog,
                ex.GetType().Name,
                ex.GetRelevantFormattedErrorCode());

            return false;
        }
    }

    public bool TryDeleteEmptyFolder(string path)
    {
        if (NonEmptyFolderExists(path))
        {
            _logger.LogWarning("Local folder is not empty, skipping deletion");
            return true;
        }

        try
        {
            Directory.Delete(path, recursive: false);
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
        catch (IOException exception) when (exception.HResultContainsWin32ErrorCode(Win32SystemErrorCode.ErrorInvalidName))
        {
            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            _logger.LogWarning("Failed to delete local folder: {ExceptionType} {ErrorCode}", ex.GetType().Name, ex.GetRelevantFormattedErrorCode());

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
            return null;
        }

        foreach (var folderNameCandidate in _numberSuffixedNameGenerator.GenerateNames(username, NameType.Folder))
        {
            var path = Path.Combine(userDataPath, folderNameCandidate);

            if (!NonEmptyFolderExists(path))
            {
                return path;
            }
        }

        _logger.LogWarning("Failed to generate a unique account root folder path");
        return null;
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
                "Failed to convert local folder \"{Path}\" to placeholder: {ExceptionType} {ErrorCode}",
                pathToLog,
                ex.GetType().Name,
                ex.GetRelevantFormattedErrorCode());

            return false;
        }
    }

    public bool TrySetPinState(string path, FilePinState pinState)
    {
        try
        {
            using var fileSystemObject = FileSystemObject.Open(
                path,
                FileMode.Open,
                FileSystemFileAccess.WriteAttributes,
                FileShare.ReadWrite | FileShare.Delete,
                FileOptions.None);

            fileSystemObject.SetPinState(ToCfPinState(pinState), CF_SET_PIN_FLAGS.CF_SET_PIN_FLAG_RECURSE);

            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is COMException)
        {
            var pathToLog = _logger.GetSensitiveValueForLogging(path);

            _logger.LogWarning(
                "Failed to set pin state {PinState} for local folder \"{Path}\": {ExceptionType} {ErrorCode}",
                pinState,
                pathToLog,
                ex.GetType().Name,
                ex.GetRelevantFormattedErrorCode());

            return false;
        }
    }

    public bool TryGetPinState(string path, out FilePinState pinState)
    {
        try
        {
            using var fileSystemObject = FileSystemObject.Open(
                path,
                FileMode.Open,
                FileSystemFileAccess.ReadAttributes,
                FileShare.ReadWrite | FileShare.Delete,
                FileOptions.Asynchronous);

            pinState = ToFilePinState(fileSystemObject.Attributes);

            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            var pathToLog = _logger.GetSensitiveValueForLogging(path);

            _logger.LogWarning(
                "Failed to obtain pin state for local folder \"{Path}\": {ExceptionType} {ErrorCode}",
                pathToLog,
                ex.GetType().Name,
                ex.GetRelevantFormattedErrorCode());

            pinState = default;

            return false;
        }
    }

    private static CF_PIN_STATE ToCfPinState(FilePinState pinState)
    {
        return pinState switch
        {
            FilePinState.Unspecified => CF_PIN_STATE.CF_PIN_STATE_UNSPECIFIED,
            FilePinState.Pinned => CF_PIN_STATE.CF_PIN_STATE_PINNED,
            FilePinState.DehydrationRequested => CF_PIN_STATE.CF_PIN_STATE_UNPINNED,
            FilePinState.Excluded => CF_PIN_STATE.CF_PIN_STATE_EXCLUDED,
            _ => throw new ArgumentOutOfRangeException(nameof(pinState), pinState, null),
        };
    }

    private static FilePinState ToFilePinState(FileAttributes attributes)
    {
        if (attributes.IsPinned())
        {
            return FilePinState.Pinned;
        }

        if (attributes.IsDehydrationRequested())
        {
            return FilePinState.DehydrationRequested;
        }

        if (attributes.IsExcluded())
        {
            return FilePinState.Excluded;
        }

        return FilePinState.DehydrationRequested;
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
        catch (Exception ex) when (ex.IsFileAccessException() || ex is Win32Exception)
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
        catch (Exception ex) when (ex.IsFileAccessException() || ex is Win32Exception)
        {
            return false;
        }

        return true;
    }
}
