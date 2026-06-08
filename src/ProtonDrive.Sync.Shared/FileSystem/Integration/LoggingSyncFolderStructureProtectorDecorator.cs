using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Sync.Shared.FileSystem.Integration;

public sealed class LoggingSyncFolderStructureProtectorDecorator : ISyncFolderStructureProtector
{
    private readonly ILogger<LoggingSyncFolderStructureProtectorDecorator> _logger;
    private readonly ISyncFolderStructureProtector _decoratedInstance;

    public LoggingSyncFolderStructureProtectorDecorator(
        ILogger<LoggingSyncFolderStructureProtectorDecorator> logger,
        ISyncFolderStructureProtector decoratedInstance)
    {
        _logger = logger;
        _decoratedInstance = decoratedInstance;
    }

    public bool ProtectFolder(string folderPath, FolderProtectionType protectionType)
    {
        _logger.LogDebug("Adding folder \"{Path}\" protection", folderPath);

        try
        {
            return _decoratedInstance.ProtectFolder(folderPath, protectionType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to add folder protection: {ExceptionType} {ErrorCode}", ex.GetType().Name, ex.GetRelevantFormattedErrorCode());

            throw;
        }
    }

    public bool UnprotectFolder(string folderPath, FolderProtectionType protectionType)
    {
        _logger.LogDebug("Removing folder \"{Path}\" protection", folderPath);

        try
        {
            return _decoratedInstance.UnprotectFolder(folderPath, protectionType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to remove folder protection: {ExceptionType} {ErrorCode}", ex.GetType().Name, ex.GetRelevantFormattedErrorCode());

            throw;
        }
    }

    public bool ProtectFile(string filePath, FileProtectionType protectionType)
    {
        _logger.LogDebug("Adding file \"{Path}\" protection", filePath);

        try
        {
            return _decoratedInstance.ProtectFile(filePath, protectionType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to add file protection: {ExceptionType} {ErrorCode}", ex.GetType().Name, ex.GetRelevantFormattedErrorCode());

            throw;
        }
    }

    public bool UnprotectFile(string filePath, FileProtectionType protectionType)
    {
        _logger.LogDebug("Removing file \"{Path}\" protection", filePath);

        try
        {
            return _decoratedInstance.UnprotectFile(filePath, protectionType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to remove file protection: {ExceptionType} {ErrorCode}", ex.GetType().Name, ex.GetRelevantFormattedErrorCode());

            throw;
        }
    }

    public bool UnprotectBranch(string folderPath, FolderProtectionType folderProtectionType, FileProtectionType fileProtectionType)
    {
        _logger.LogDebug("Removing branch \"{Path}\" protection", folderPath);

        try
        {
            return _decoratedInstance.UnprotectBranch(folderPath, folderProtectionType, fileProtectionType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to remove branch protection: {ExceptionType} {ErrorCode}", ex.GetType().Name, ex.GetRelevantFormattedErrorCode());

            throw;
        }
    }
}
