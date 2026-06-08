using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Shared.FileSystem.Integration;

namespace ProtonDrive.Sync.Windows.FileSystem.Integration;

public sealed class ReadOnlyFileAttributeRemover : IReadOnlyFileAttributeRemover
{
    private readonly ILogger<ReadOnlyFileAttributeRemover> _logger;

    private readonly EnumerationOptions _enumerationOptions = new()
    {
        RecurseSubdirectories = false,
        AttributesToSkip = FileAttributes.None, // By default, Hidden and System attributes are skipped
    };

    public ReadOnlyFileAttributeRemover(ILogger<ReadOnlyFileAttributeRemover> logger)
    {
        _logger = logger;
    }

    public bool TryRemoveFileReadOnlyAttributeInFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return true;
        }

        try
        {
            RemoveFileReadOnlyAttributesInFolder(folderPath);

            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            _logger.LogError("Failed to remove file read-only attribute: {ExceptionType}: {ErrorCode}", ex.GetType().Name, ex.GetRelevantFormattedErrorCode());

            return false;
        }
    }

    public bool TryRemoveFileReadOnlyAttribute(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return true;
        }

        try
        {
            RemoveReadOnlyAttribute(filePath);

            return true;
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            _logger.LogError("Failed to remove file read-only attribute: {ExceptionType}: {ErrorCode}", ex.GetType().Name, ex.GetRelevantFormattedErrorCode());

            return false;
        }
    }

    private static void RemoveReadOnlyAttribute(string filePath)
    {
        using var file = FileSystemFile.Open(filePath, FileSystemFileAccess.WriteAttributes);

        if (file.Attributes.HasFlag(FileAttributes.ReadOnly))
        {
            file.Attributes &= ~FileAttributes.ReadOnly;
        }
    }

    private void RemoveFileReadOnlyAttributesInFolder(string folderPath)
    {
        using var folder = FileSystemDirectory.Open(folderPath, FileSystemFileAccess.Read);

        var entries = folder.EnumerateFileSystemEntries(options: _enumerationOptions);

        foreach (var entry in entries)
        {
            var entryFullPath = Path.Combine(folder.FullPath, entry.Name);

            if (entry.Attributes.HasFlag(FileAttributes.Directory))
            {
                RemoveFileReadOnlyAttributesInFolder(entryFullPath);
            }
            else
            {
                RemoveReadOnlyAttribute(entryFullPath);
            }
        }
    }
}
