using Proton.Drive.Sdk.Nodes.Download;
using Proton.Drive.Sdk.Nodes.Upload;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Client.Sdk;

internal static class ResilienceExtensions
{
    public static async ValueTask ExecuteWithRetryAsync(this UploadController controller, int numberOfRetries, TimeSpan delay, CancellationToken cancellationToken)
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
            }

            if (!controller.IsPaused)
            {
                return;
            }
        }
    }

    public static async ValueTask ExecuteWithRetryAsync(this DownloadController controller, int numberOfRetries, TimeSpan delay, CancellationToken cancellationToken)
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
            }

            if (!controller.IsPaused)
            {
                return;
            }
        }
    }
}
