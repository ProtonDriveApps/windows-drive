using System;
using System.IO;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Sync;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Windows.FileSystem;

namespace ProtonDrive.App.Windows.SystemIntegration;

internal sealed class FileSystemIdentityProvider : IFileSystemIdentityProvider<long>
{
    private readonly ILogger<FileSystemIdentityProvider> _logger;

    public FileSystemIdentityProvider(ILogger<FileSystemIdentityProvider> logger)
    {
        _logger = logger;
    }

    public bool TryGetIdFromPath(string path, out long id)
    {
        try
        {
            using var fileSystemObject = FileSystemObject.Open(
                path,
                FileMode.Open,
                FileSystemFileAccess.None,
                FileShare.ReadWrite | FileShare.Delete,
                FileOptions.None);

            id = fileSystemObject.ObjectId;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Could not get identity of file system object: {ExceptionType} {ErrorCode}",
                ex.GetType().Name,
                ex.GetRelevantFormattedErrorCode());

            id = default;
            return false;
        }
    }
}
