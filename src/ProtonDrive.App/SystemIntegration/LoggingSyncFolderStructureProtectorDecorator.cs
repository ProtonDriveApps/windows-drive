using System;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;

namespace ProtonDrive.App.SystemIntegration;

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

    public bool Protect(string folderPath, FolderProtectionType protectionType)
    {
        var pathToLog = _logger.GetSensitiveValueForLogging(folderPath);
        _logger.LogInformation("Adding folder \"{Path}\" protection", pathToLog);

        try
        {
            return _decoratedInstance.Protect(folderPath, protectionType);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to add folder protection: {Message}", ex.CombinedMessage());

            throw;
        }
    }

    public bool Unprotect(string folderPath, FolderProtectionType protectionType)
    {
        var pathToLog = _logger.GetSensitiveValueForLogging(folderPath);
        _logger.LogInformation("Removing folder \"{Path}\" protection", pathToLog);

        try
        {
            return _decoratedInstance.Unprotect(folderPath, protectionType);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to remove folder protection: {Message}", ex.CombinedMessage());

            throw;
        }
    }
}
