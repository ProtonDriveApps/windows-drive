namespace Proton.Drive.Sdk.Sync.Agent.Services;

public interface IStartableService
{
    public Task StartAsync(CancellationToken cancellationToken);
}
