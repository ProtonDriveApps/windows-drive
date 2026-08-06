using Proton.Drive.Sdk.Sync.Client.Health.Contracts;
using Proton.Drive.Shared.Client;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client.Health;

internal interface IDriveHealthApiClient
{
    [Get("/health/hash-check")]
    [BearerAuthorizationHeader]
    public Task<FileConsistencyCheckResponse> GetFileConsistencyCheckIsRequiredAsync([AliasAs("ClientUID")] string clientInstanceId, CancellationToken cancellationToken);

    [Post("/health/hash-check")]
    [BearerAuthorizationHeader]
    public Task<ApiResponse> UpdateFileConsistencyCheckStatusAsync(FileConsistencyCheckStatus status, CancellationToken cancellationToken);
}
