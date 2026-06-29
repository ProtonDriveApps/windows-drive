using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Nodes.Download;
using Proton.Drive.Sdk.Nodes.Upload;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Client.Sdk;

internal static class ResilienceExtensions
{
    public static async ValueTask ExecuteWithRetryAsync(this UploadController controller, int numberOfRetries, TimeSpan delay, ILogger logger, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 1 + numberOfRetries; attempt++)
        {
            if (controller.IsPaused)
            {
                await Task.Delay(delay.RandomizedWithDeviation(0.2), cancellationToken).ConfigureAwait(false);

                controller.Resume();
            }

            try
            {
                await controller.Completion.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (controller.IsPaused)
                {
                    logger.LogWarning("Drive SDK paused file upload: {ErrorMessage}", ex.CombinedMessage());
                }
            }

            if (!controller.IsPaused)
            {
                return;
            }
        }
    }

    public static async ValueTask ExecuteWithRetryAsync(this DownloadController controller, int numberOfRetries, TimeSpan delay, ILogger logger, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 1 + numberOfRetries; attempt++)
        {
            if (controller.IsPaused)
            {
                await Task.Delay(delay.RandomizedWithDeviation(0.2), cancellationToken).ConfigureAwait(false);

                controller.Resume();
            }

            try
            {
                await controller.Completion.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (controller.IsPaused)
                {
                    logger.LogWarning("Drive SDK paused file download: {ErrorMessage}", ex.CombinedMessage());
                }
            }

            if (!controller.IsPaused)
            {
                return;
            }
        }
    }
}
