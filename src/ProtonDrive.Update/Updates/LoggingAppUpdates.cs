using System;
using Microsoft.Extensions.Logging;

namespace ProtonDrive.Update.Updates;

/// <summary>
/// Logs requests to <see cref="IAppUpdates"/>.
/// </summary>
internal class LoggingAppUpdates : IAppUpdates
{
    private readonly ILogger<LoggingAppUpdates> _logger;
    private readonly IAppUpdates _origin;

    public LoggingAppUpdates(ILogger<LoggingAppUpdates> logger, IAppUpdates origin)
    {
        _logger = logger;
        _origin = origin;
    }

    public void Cleanup()
    {
        try
        {
            _logger.LogInformation("Started cleaning up downloaded app updates");

            _origin.Cleanup();

            _logger.LogInformation("Finished cleaning up downloaded app updates");
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to cleanup downloaded app updates: {ExceptionType} {HResult}", e.GetType().Name, e.HResult);

            throw;
        }
    }
}
