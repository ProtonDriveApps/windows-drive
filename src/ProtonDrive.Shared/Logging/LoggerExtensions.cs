using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.Shared.Logging;

public static class LoggerExtensions
{
    public static string GetSensitiveValueForLogging(this ILogger logger, string sensitiveValue)
    {
        return logger.IsEnabled(LogLevel.Debug) ? sensitiveValue : "<private>";
    }

    public static CoalescingAction GetCoalescingActionWithExceptionsLoggingAndCancellationHandling(this ILogger logger, Func<CancellationToken, Task> origin, string componentName)
    {
        return new CoalescingAction(
            ct => logger.WithLoggedException(
                () => logger.WithSafeCancellation(() => origin(ct), componentName),
                $"{componentName} operation failed",
                includeStackTrace: true));
    }

    public static SingleAction GetSingleActionWithExceptionsLoggingAndCancellationHandling(this ILogger logger, Func<CancellationToken, Task> origin, string componentName)
    {
        return new SingleAction(
            ct => logger.WithLoggedException(
                () => logger.WithSafeCancellation(() => origin(ct), componentName),
                $"{componentName} operation failed",
                includeStackTrace: true));
    }
}
