using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Shared.ExecutionStatistics;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Features;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection;

internal sealed class BulkDeletionDetectingUpdateDetectorDecorator(
    IUpdateDetector updateDetector,
    IBulkDeletionDetector bulkDeletionDetector,
    TimeSpan syncDelayInterval,
    IFeatureFlagProvider featureFlagProvider,
    IClock clock,
    ILogger<BulkDeletionDetectingUpdateDetectorDecorator> logger)
    : IUpdateDetector
{
    public IExecutionStatistics ExecutionStatistics => updateDetector.ExecutionStatistics;

    public Task StartAsync(CancellationToken cancellationToken) => updateDetector.StartAsync(cancellationToken);

    public Task StopAsync() => updateDetector.StopAsync();

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        BeginDetectingBulkDeletion();

        do
        {
            await updateDetector.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
        while (await ContinueDetectingBulkDeletionAsync(cancellationToken).ConfigureAwait(false));
    }

    private void BeginDetectingBulkDeletion()
    {
        bulkDeletionDetector.BeginDetection(updateDetector.ExecutionStatistics);
    }

    private async Task<bool> ContinueDetectingBulkDeletionAsync(CancellationToken cancellationToken)
    {
        if (!await FeatureIsEnabledAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (!bulkDeletionDetector.ContinueDetection(updateDetector.ExecutionStatistics))
        {
            return false;
        }

        logger.LogWarning("Bulk deletion detection is in progress...");

        await clock.DelayAsync(syncDelayInterval, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private async Task<bool> FeatureIsEnabledAsync(CancellationToken cancellationToken)
    {
        return !await featureFlagProvider.IsEnabledAsync(Feature.DriveWindowsBulkDeletionSquashingDisabled, cancellationToken).ConfigureAwait(false);
    }
}
