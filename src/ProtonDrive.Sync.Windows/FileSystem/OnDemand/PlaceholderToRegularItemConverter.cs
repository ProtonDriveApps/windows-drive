using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem.OnDemand;
using static Vanara.PInvoke.CldApi;

namespace ProtonDrive.Sync.Windows.FileSystem.OnDemand;

public sealed class PlaceholderToRegularItemConverter : IPlaceholderToRegularItemConverter
{
    private readonly ILogger<PlaceholderToRegularItemConverter> _logger;

    private readonly EnumerationOptions _enumerationOptions = new()
    {
        RecurseSubdirectories = false,
        AttributesToSkip = FileAttributes.None, // By default, Hidden and System attributes are skipped
    };

    public PlaceholderToRegularItemConverter(ILogger<PlaceholderToRegularItemConverter> logger)
    {
        _logger = logger;
    }

    public bool TryConvertToRegularFolder(string path, bool skipRoot)
    {
        if (!Directory.Exists(path))
        {
            return true;
        }

        try
        {
            TryConvertFolder(path, skipRoot, out _);

            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is COMException)
        {
            _logger.LogError("Failed to convert to classic folder: {ExceptionType}: {ErrorCode}", ex.GetType().Name, ex.GetRelevantFormattedErrorCode());

            return false;
        }
    }

    public bool TryConvertToRegularFile(string path)
    {
        if (!File.Exists(path))
        {
            return true;
        }

        try
        {
            TryConvertFile(path, out _);

            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is COMException)
        {
            _logger.LogError("Failed to convert to classic file: {ExceptionType}: {ErrorCode}", ex.GetType().Name, ex.GetRelevantFormattedErrorCode());

            return false;
        }
    }

    private static bool IsPlaceholder(FileSystemObject fileSystemObject)
    {
        var placeholderState = fileSystemObject.GetPlaceholderState();

        return placeholderState.HasFlag(PlaceholderState.Placeholder);
    }

    private static bool IsPartial(FileSystemObject fileSystemObject)
    {
        var placeholderState = fileSystemObject.GetPlaceholderState();

        return placeholderState.HasFlag(PlaceholderState.Partial);
    }

    private static void RemoveReadOnlyAttribute(FileSystemFile file)
    {
        if (file.Attributes.HasFlag(FileAttributes.ReadOnly))
        {
            file.Attributes &= ~FileAttributes.ReadOnly;
        }
    }

    private void TryConvertFile(string filePath, out bool deleted)
    {
        deleted = false;

        try
        {
            using var file = FileSystemFile.Open(filePath, FileSystemFileAccess.WriteAttributes | FileSystemFileAccess.Delete);

            if (TryDeletePartialFile(file))
            {
                deleted = true;
                return;
            }

            TryRevertPlaceholder(file);
            TryExcludeFromSync(file);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            deleted = true;
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is COMException)
        {
            _logger.LogWarning("Failed to convert file: {ExceptionType}: {ErrorCode}", ex.GetType().Name, ex.GetRelevantFormattedErrorCode());
        }
    }

    private void TryConvertFolder(string folderPath, bool skipRoot, out bool deleted)
    {
        deleted = false;

        try
        {
            using var folder = FileSystemDirectory.Open(folderPath, FileSystemFileAccess.Read | FileSystemFileAccess.Delete);

            var entries = folder.EnumerateFileSystemEntries(options: _enumerationOptions);

            var folderIsEmpty = true;

            foreach (var entry in entries)
            {
                if (entry.Attributes.HasFlag(FileAttributes.Hidden | FileAttributes.System))
                {
                    // A protected operating system file or folder. It is not synced, and we ignore it here.
                    folderIsEmpty = false;
                    continue;
                }

                var entryFullPath = Path.Combine(folder.FullPath, entry.Name);

                bool entryDeleted;

                if (entry.Attributes.HasFlag(FileAttributes.Directory))
                {
                    TryConvertFolder(entryFullPath, skipRoot: false, out entryDeleted);
                }
                else
                {
                    TryConvertFile(entryFullPath, out entryDeleted);
                }

                folderIsEmpty &= entryDeleted;
            }

            if (skipRoot)
            {
                return;
            }

            if (folderIsEmpty && TryDeleteEmptyFolder(folder))
            {
                deleted = true;
                return;
            }

            TryRevertPlaceholder(folder);
            TryExcludeFromSync(folder);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            deleted = true;
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is COMException)
        {
            _logger.LogWarning("Failed to convert folder: {ExceptionType}: {ErrorCode}", ex.GetType().Name, ex.GetRelevantFormattedErrorCode());
        }
    }

    private bool TryDeletePartialFile(FileSystemFile file)
    {
        try
        {
            if (!IsPartial(file))
            {
                return false;
            }

            RemoveReadOnlyAttribute(file);
            file.Delete();

            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is COMException)
        {
            _logger.LogWarning("Failed to delete partial file: {ExceptionType}: {ErrorCode}", ex.GetType().Name, ex.GetRelevantFormattedErrorCode());
            return false;
        }
    }

    private bool TryDeleteEmptyFolder(FileSystemDirectory folder)
    {
        try
        {
            if (folder.Attributes.IsPinned() || folder.Attributes.IsExcluded())
            {
                return false;
            }

            folder.Delete(recursive: false);

            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is COMException)
        {
            _logger.LogWarning("Failed to delete empty folder: {ExceptionType}: {ErrorCode}", ex.GetType().Name, ex.GetRelevantFormattedErrorCode());
            return false;
        }
    }

    private void TryRevertPlaceholder(FileSystemObject fileSystemObject)
    {
        try
        {
            if (IsPlaceholder(fileSystemObject))
            {
                fileSystemObject.RevertPlaceholder();
            }
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is COMException)
        {
            _logger.LogWarning("Failed to revert placeholder: {ExceptionType}: {ErrorCode}", ex.GetType().Name, ex.GetRelevantFormattedErrorCode());
        }
    }

    private void TryExcludeFromSync(FileSystemObject fileSystemObject)
    {
        try
        {
            fileSystemObject.SetPinState(CF_PIN_STATE.CF_PIN_STATE_EXCLUDED, CF_SET_PIN_FLAGS.CF_SET_PIN_FLAG_NONE);
        }
        catch (COMException ex) when (ex.HResultContainsWin32ErrorCode(Win32SystemErrorCode.ErrorInvalidFunction))
        {
            // The sync root is not registered
        }
        catch (Exception ex) when (ex.IsFileAccessException() || ex is COMException)
        {
            _logger.LogWarning("Failed to exclude from sync: {ExceptionType}: {ErrorCode}", ex.GetType().Name, ex.GetRelevantFormattedErrorCode());
        }
    }
}
