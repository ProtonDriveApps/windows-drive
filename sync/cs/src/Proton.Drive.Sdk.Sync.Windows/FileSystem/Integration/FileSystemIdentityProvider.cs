using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Integration;

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
