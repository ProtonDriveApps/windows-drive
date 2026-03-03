using ProtonDrive.Client.Health.Contracts;
using Refit;

namespace ProtonDrive.Client.Health;

internal interface IDriveHealthApiClient
{
    [Get("/health/hash-check")]
    [BearerAuthorizationHeader]
    public Task<FileConsistencyCheckResponse> GetFileConsistencyCheckIsRequiredAsync([AliasAs("ClientUID")] string clientInstanceId, CancellationToken cancellationToken);

    [Post("/health/hash-check")]
    [BearerAuthorizationHeader]
    public Task<ApiResponse> UpdateFileConsistencyCheckStatusAsync(FileConsistencyCheckStatus status, CancellationToken cancellationToken);
}
