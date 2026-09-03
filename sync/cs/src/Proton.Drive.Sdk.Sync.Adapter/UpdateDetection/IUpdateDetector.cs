using Proton.Drive.Sdk.Sync.Shared.ExecutionStatistics;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection;

internal interface IUpdateDetector : IExecutionStatisticsProvider
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
    Task ExecuteAsync(CancellationToken cancellationToken);
}
