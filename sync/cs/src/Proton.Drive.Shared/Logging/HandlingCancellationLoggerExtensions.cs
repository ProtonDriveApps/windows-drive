using Microsoft.Extensions.Logging;

namespace Proton.Drive.Shared.Logging;

public static class HandlingCancellationLoggerExtensions
{
    public static async Task WithSafeCancellation(
        this ILogger logger,
        Func<Task> origin,
        string componentName)
    {
        try
        {
            await origin.Invoke().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
            logger.LogInformation("{ComponentName} operation was cancelled", componentName);
        }
        catch (FaultyStateException ex)
        {
            // Expected
            logger.LogWarning("{ComponentName} operation failed: {ErrorMessage}", componentName, ex.Message);
        }
    }
}
