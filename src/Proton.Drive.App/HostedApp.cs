using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Services;

namespace Proton.Drive.App;

internal class HostedApp : IHostedService
{
    private readonly IEnumerable<IStartableService> _startableServices;
    private readonly IEnumerable<IStoppableService> _stoppableServices;
    private readonly ILogger<HostedApp> _logger;

    public HostedApp(
        IEnumerable<IStartableService> startableServices,
        IEnumerable<IStoppableService> stoppableServices,
        ILogger<HostedApp> logger)
    {
        _startableServices = startableServices;
        _stoppableServices = stoppableServices;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting services...");

        foreach (var startable in _startableServices)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await startable.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Starting services completed");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping services...");

        // All stoppable app services are stopped concurrently
        await Task.WhenAll(_stoppableServices.Select(s => s.StopAsync(cancellationToken))).ConfigureAwait(false);

        _logger.LogInformation("Stopping services completed");
    }
}
