namespace ProtonDrive.Sync.Agent.Health;

public interface IFileConsistencyGuard
{
    void StartExecuting();
    Task StopExecutingAsync();
}
