using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Proton.Drive.Shared.Client;
using Proton.Drive.Shared.Net.Http;

namespace Proton.Drive.Sdk.Sync.Client.Authentication;

internal sealed class AuthorizationHandler : DelegatingHandler
{
    private const string AuthenticationUrlBase = "/auth/v4";
    private const string SessionHeaderName = "x-pm-uid";
    private const string BearerAuthorizationSchema = "Bearer";

    private readonly Lazy<IAuthenticationService> _authenticationService;
    private readonly Lazy<ISessionProvider> _sessionProvider;
    private readonly ILogger<AuthorizationHandler> _logger;

    public AuthorizationHandler(
        Lazy<IAuthenticationService> authenticationService,
        Lazy<ISessionProvider> sessionProvider,
        ILogger<AuthorizationHandler> logger)
    {
        _authenticationService = authenticationService;
        _sessionProvider = sessionProvider;
        _logger = logger;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Existence of the session header indicates necessity to pass through the request without handling authorization,
        // the authorization header should already be set.
        return request.Headers.Authorization?.Scheme == BearerAuthorizationSchema && !request.Headers.Contains(SessionHeaderName)
                ? SendWithAuthorizationOrEndSessionAsync(request, cancellationToken)
                : base.SendAsync(request, cancellationToken);
    }

    private static async Task<ApiResponse?> GetApiResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode is < HttpStatusCode.BadRequest or >= HttpStatusCode.InternalServerError)
        {
            return null;
        }

        return await response.TryReadFromJsonAsync<ApiResponse?>(cancellationToken).ConfigureAwait(false);
    }

    private static bool IsAccountDisabled([NotNullWhen(true)] ApiResponse? apiResponse)
    {
        return apiResponse is { Code: ResponseCode.AccountDeleted or ResponseCode.AccountDisabled };
    }

    private async Task<HttpResponseMessage> SendWithAuthorizationOrEndSessionAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var (sessionId, response) = await SendWithAuthorizationAsync(request, cancellationToken).ConfigureAwait(false);

        var apiResponse = await GetApiResponseAsync(response, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Awaiting this call would create a deadlock when Unauthorized (401) is returned to the sign-out API request.
            _ = _authenticationService.Value.EndSessionAsync(sessionId, apiResponse ?? new ApiResponse { Code = ResponseCode.Unauthorized });
        }
        else if (IsAccountDisabled(apiResponse))
        {
            // Awaiting this call would create a deadlock when Account disabled (10002 or 10003) is returned to the sign-out API request.
            _ = _authenticationService.Value.EndSessionAsync(sessionId, apiResponse);
        }

        return response;
    }

    private async Task<(string SessionId, HttpResponseMessage Response)> SendWithAuthorizationAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var (session, getRefreshedSessionAsync) = await _sessionProvider.Value.GetSessionAsync(cancellationToken).ConfigureAwait(false) ??
            throw new HttpRequestException("No session available for secure request.", null, HttpStatusCode.Unauthorized);

        var response = await SetAuthorizationHeaderAndSendAsync(request, session, cancellationToken).ConfigureAwait(false);

        (session, response) = await RetryWithRefreshedSessionIfUnauthorizedAsync(request, response, session, getRefreshedSessionAsync, cancellationToken)
            .ConfigureAwait(false);

        return (session.Id, response);
    }

    private async Task<(Session Session, HttpResponseMessage Response)> RetryWithRefreshedSessionIfUnauthorizedAsync(
        HttpRequestMessage request,
        HttpResponseMessage response,
        Session session,
        Func<CancellationToken, Task<Session?>> getRefreshedSessionAsync,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized && EndpointWouldBenefitFromRefresh(request.RequestUri!.PathAndQuery))
        {
            var refreshedSession = await getRefreshedSessionAsync.Invoke(cancellationToken).ConfigureAwait(false);
            if (refreshedSession != null)
            {
                session = refreshedSession;
                response = await SetAuthorizationHeaderAndSendAsync(request, refreshedSession, cancellationToken).ConfigureAwait(false);
            }
        }

        return (session, response);

        static bool EndpointWouldBenefitFromRefresh(string pathAndQuery)
            => !pathAndQuery.StartsWith(AuthenticationUrlBase) || pathAndQuery is $"{AuthenticationUrlBase}/scopes";
    }

    private async Task<HttpResponseMessage> SetAuthorizationHeaderAndSendAsync(HttpRequestMessage request, Session session, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(request.Headers.Authorization?.Scheme ?? BearerAuthorizationSchema, session.AccessToken);

        var requestDidNotContainSessionHeader = !request.Headers.Remove(SessionHeaderName);
        request.Headers.Add(SessionHeaderName, session.Id);

        try
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var accessToken = request.Headers.Authorization?.Parameter;

                _logger.LogWarning(
                    "Failure response : ______ {Method} {Uri} - {StatusCode} (access token \"{AccessTokenFingerprint:x4}\")",
                    request.Method,
                    request.RequestUri,
                    (int)response.StatusCode,
                    accessToken?.GetHashCode() & 0xFFFF);
            }

            return response;
        }
        finally
        {
            if (requestDidNotContainSessionHeader)
            {
                // Existence of the session header prevents this handler from handling authorization.
                // We remove the header, so that if the same request is retried, it is handled the same way as before.
                request.Headers.Remove(SessionHeaderName);
            }
        }
    }
}
