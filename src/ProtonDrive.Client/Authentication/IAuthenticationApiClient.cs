using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Authentication.Contracts;
using Refit;

namespace ProtonDrive.Client.Authentication;

internal interface IAuthenticationApiClient
{
    [Post("/v4/info")]
    Task<AuthInfo> GetAuthInfoAsync(AuthInfoRequest data, CancellationToken cancellationToken);

    [Post("/v4")]
    Task<AuthResponse> LoginAsync(AuthRequest data, CancellationToken cancellationToken);

    [Post("/v4/2fa")]
    [BearerAuthorizationHeader]
    Task<ScopesResponse> LoginAsync(AuthSecondFactorRequest data, CancellationToken cancellationToken);

    [Delete("/v4")]
    Task<ApiResponse> LogoutAsync(
        [Header("x-pm-uid")] string sessionId,
        [Authorize] string accessToken,
        CancellationToken cancellationToken);

    [Post("/v4/refresh")]
    Task<RefreshSessionResponse> RefreshSessionAsync(
        RefreshSessionParameters parameters,
        [Header("x-pm-uid")] string sessionId,
        [Authorize] string accessToken,
        CancellationToken cancellationToken);

    [Get("/v4/scopes")]
    [BearerAuthorizationHeader]
    Task<ScopesResponse> GetScopesAsync(CancellationToken cancellationToken);

    [Get("/v4/modulus")]
    [BearerAuthorizationHeader]
    Task<ModulusResponse> GetRandomSrpModulusAsync(CancellationToken cancellationToken);
}
