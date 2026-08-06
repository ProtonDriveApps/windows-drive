namespace Proton.Drive.Sdk.Sync.Client.Health;

public interface IDriveHealthClient
{
    Task<bool> GetFileConsistencyCheckIsRequiredAsync(string clientInstanceId, CancellationToken cancellationToken);
    Task StartFileConsistencyCheckAsync(string clientInstanceId, CancellationToken cancellationToken);
    Task FinishFileConsistencyCheckAsync(string clientInstanceId, FileConsistencyCheckResult result, CancellationToken cancellationToken);
}
