namespace Proton.Drive.Sdk.Sync.Agent.Services;

public interface IStoppableService
{
    public Task StopAsync(CancellationToken cancellationToken);
}
