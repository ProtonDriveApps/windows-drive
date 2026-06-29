using Microsoft.Extensions.Logging;
using Proton.Drive.Shared.Logging;

namespace Proton.Drive.Shared.Threading;

public sealed class LoggingExceptionsSchedulerDecorator : IScheduler
{
    private readonly ILogger _logger;
    private readonly IScheduler _decoratedInstance;

    private readonly string _logMessage;

    public LoggingExceptionsSchedulerDecorator(string subjectName, ILogger logger, IScheduler decoratedInstance)
    {
        _logger = logger;
        _decoratedInstance = decoratedInstance;

        _logMessage = $"{subjectName} failed with exception";
    }

    public Task<T> Schedule<T>(Func<Task<T>> function)
    {
        return _decoratedInstance.Schedule(() => _logger.WithLoggedException(function, _logMessage, includeStackTrace: true));
    }

    public ISchedulerTimer CreateTimer()
    {
        // Logging of timer event handler exceptions is not supported
        return _decoratedInstance.CreateTimer();
    }
}
