using ProtonDrive.Client.Health.Contracts;

namespace ProtonDrive.Client.Health;

internal sealed class DriveHealthClient(IDriveHealthApiClient apiClient) : IDriveHealthClient
{
    public async Task<bool> GetFileConsistencyCheckIsRequiredAsync(string clientInstanceId, CancellationToken cancellationToken)
    {
        var response = await apiClient.GetFileConsistencyCheckIsRequiredAsync(clientInstanceId, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        return response.IsRequired;
    }

    public async Task StartFileConsistencyCheckAsync(string clientInstanceId, CancellationToken cancellationToken)
    {
        var status = new FileConsistencyCheckStatus
        {
            ClientInstanceId = clientInstanceId,
            State = FileConsistencyCheckState.Ongoing,
        };

        await apiClient.UpdateFileConsistencyCheckStatusAsync(status, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
    }

    public async Task FinishFileConsistencyCheckAsync(string clientInstanceId, FileConsistencyCheckResult result, CancellationToken cancellationToken)
    {
        var status = new FileConsistencyCheckStatus
        {
            ClientInstanceId = clientInstanceId,
            InspectedItemCount = result.InspectedItemCount,
            RefreshedItemCount = result.RefreshedItemCount,
            FailedItemCount = result.FailedItemCount,
            State = result.FailedItemCount == 0 ? FileConsistencyCheckState.Finished : FileConsistencyCheckState.Failed,
        };

        await apiClient.UpdateFileConsistencyCheckStatusAsync(status, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
    }
}
