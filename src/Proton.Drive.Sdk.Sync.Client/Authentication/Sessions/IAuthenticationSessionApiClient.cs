using Proton.Drive.Shared.Client;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client.Authentication.Sessions;

internal interface IAuthenticationSessionApiClient
{
    [Post("/v4/sessions/forks")]
    [BearerAuthorizationHeader]
    Task<SessionForkingResponse> ForkSessionAsync(SessionForkingParameters parameters, CancellationToken cancellationToken);

    [Delete("/v4/sessions/forks/{sessionSelector}")]
    [BearerAuthorizationHeader]
    Task<ApiResponse> InvalidateSessionForkAsync(string sessionSelector, CancellationToken cancellationToken);
}
