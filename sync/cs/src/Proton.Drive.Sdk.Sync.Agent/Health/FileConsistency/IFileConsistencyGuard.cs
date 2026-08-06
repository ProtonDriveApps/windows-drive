namespace Proton.Drive.Sdk.Sync.Agent.Health.FileConsistency;

public interface IFileConsistencyGuard
{
    void StartExecuting();
    Task StopExecutingAsync();
}
