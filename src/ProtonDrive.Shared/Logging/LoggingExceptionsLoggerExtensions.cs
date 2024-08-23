using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Shared.Logging;

public static class LoggingExceptionsLoggerExtensions
{
    public static void WithLoggedException(this ILogger logger, Action origin, bool includeStackTrace = false)
    {
        WithLoggedException(logger, origin, () => "Failed with exception", includeStackTrace);
    }

    public static void WithLoggedException(this ILogger logger, Action origin, string message, bool includeStackTrace = false)
    {
        WithLoggedException(logger, origin, () => message, includeStackTrace);
    }

    public static void WithLoggedException(this ILogger logger, Action origin, Func<string> message, bool includeStackTrace = false)
    {
        bool Function()
        {
            origin();
            return false;
        }

        WithLoggedException(logger, Function, message, includeStackTrace);
    }

    public static T WithLoggedException<T>(this ILogger logger, Func<T> origin, bool includeStackTrace = false)
    {
        return WithLoggedException(logger, origin, () => "Failed with exception", includeStackTrace);
    }

    public static T WithLoggedException<T>(this ILogger logger, Func<T> origin, string message, bool includeStackTrace = false)
    {
        return WithLoggedException(logger, origin, () => message, includeStackTrace);
    }

    public static T WithLoggedException<T>(this ILogger logger, Func<T> origin, Func<string> message, bool includeStackTrace = false)
    {
        try
        {
            return origin();
        }
        catch (Exception ex)
        {
            if (includeStackTrace)
            {
                logger.LogError(ex, $"{message()}: {{Message}}", ex.CombinedMessage());
            }
            else
            {
                logger.LogError($"{message()}: {{Message}}", ex.CombinedMessage());
            }

            throw;
        }
    }

    public static Task WithLoggedException(this ILogger logger, Func<Task> origin, bool includeStackTrace = false)
    {
        return WithLoggedException(logger, origin, () => "Failed with exception", includeStackTrace);
    }

    public static Task WithLoggedException(this ILogger logger, Func<Task> origin, string message, bool includeStackTrace = false)
    {
        return WithLoggedException(logger, origin, () => message, includeStackTrace);
    }

    public static Task WithLoggedException(this ILogger logger, Func<Task> origin, Func<string> message, bool includeStackTrace = false)
    {
        async Task<Void> Function()
        {
            await origin().ConfigureAwait(false);

            return default;
        }

        return WithLoggedException(logger, Function, message, includeStackTrace);
    }

    public static Task<T> WithLoggedException<T>(this ILogger logger, Func<Task<T>> origin, bool includeStackTrace = false)
    {
        return WithLoggedException(logger, origin, () => "Failed with exception", includeStackTrace);
    }

    public static Task<T> WithLoggedException<T>(this ILogger logger, Func<Task<T>> origin, string message, bool includeStackTrace = false)
    {
        return WithLoggedException(logger, origin, () => message, includeStackTrace);
    }

    public static async Task<T> WithLoggedException<T>(this ILogger logger, Func<Task<T>> origin, Func<string> message, bool includeStackTrace = false)
    {
        try
        {
            return await origin().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (includeStackTrace)
            {
                logger.LogError(ex, $"{message()}: {{Message}}", ex.CombinedMessage());
            }
            else
            {
                logger.LogError($"{message()}: {{Message}}", ex.CombinedMessage());
            }

            throw;
        }
    }

    private struct Void { }
}
