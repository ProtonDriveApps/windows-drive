using Microsoft.Extensions.Logging;

namespace Proton.Drive.Update.Updates;

/// <summary>
/// Logs requests to <see cref="IAppUpdateCleanup"/>.
/// </summary>
internal class LoggingAppUpdater : IAppUpdateCleanup
{
    private readonly ILogger<LoggingAppUpdater> _logger;
    private readonly IAppUpdateCleanup _origin;

    public LoggingAppUpdater(ILogger<LoggingAppUpdater> logger, IAppUpdateCleanup origin)
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
