using System.Net;
using System.Security;
using Microsoft.Extensions.Logging;
using Proton.Cryptography.Srp;
using Proton.Drive.Sdk.Sync.Client.Authentication.Contracts;
using Proton.Drive.Sdk.Sync.Client.Authentication.Contracts.Fido2;
using Proton.Drive.Sdk.Sync.Client.Authentication.Srp;
using Proton.Drive.Sdk.Sync.Client.Cryptography;
using Proton.Drive.Shared.Authentication;
using Proton.Drive.Shared.Caching;
using Proton.Drive.Shared.Client;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Logging;
using Proton.Drive.Shared.Repository;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Client.Authentication;

internal sealed class AuthenticationService : IAuthenticationService, ISessionProvider
{
    private const int ServerEphemeralLength = 2048;

    private readonly IAuthenticationApiClient _authenticationApiClient;
    private readonly ISrpClientFactory _srpClientFactory;
    private readonly IUserClient _userClient;
    private readonly IAddressKeyProvider _addressKeyProvider;
    private readonly IKeyPassphraseProvider _keyPassphraseProvider;
    private readonly IProtectedRepository<Session> _sessionRepository;
    private readonly IClearableMemoryCache _cache;
    private readonly ILogger<AuthenticationService> _logger;

    private readonly SingleAction _endSession;
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);

    private SecureString? _password;
    private volatile Session? _tempSession;
    private volatile Session? _session;
    private volatile Lazy<Task> _refreshTask = new(() => Task.CompletedTask);
    private ApiResponse? _sessionEndedImplicitlyResponse;

    public AuthenticationService(
        IAuthenticationApiClient authenticationApiClient,
        ISrpClientFactory srpClientFactory,
        IUserClient userClient,
        IAddressKeyProvider addressKeyProvider,
        IKeyPassphraseProvider keyPassphraseProvider,
        IProtectedRepository<Session> sessionRepository,
        IClearableMemoryCache cache,
        ILogger<AuthenticationService> logger)
    {
        _authenticationApiClient = authenticationApiClient;
        _srpClientFactory = srpClientFactory;
        _userClient = userClient;
        _addressKeyProvider = addressKeyProvider;
        _keyPassphraseProvider = keyPassphraseProvider;
        _sessionRepository = sessionRepository;
        _cache = cache;
        _logger = logger;

        _endSession = new SingleAction(EndSessionInternalAsync);
    }

    /// <summary>
    /// Session started.
    /// The event is raised when resumed the persisted session or started a new one.
    /// </summary>
    public event EventHandler<StartSessionResult>? SessionStarted;

    /// <summary>
    /// Session ended implicitly (the API closed the session).
    /// The event is not raised when session ends explicitly (by the request from the app or user).
    /// </summary>
    public event EventHandler<ApiResponse>? SessionEndedImplicitly;

    private Session? Session
    {
        get
        {
            return _session ?? _tempSession;
        }
        set
        {
            _tempSession = null;
            _session = value;
            _sessionRepository.Set(value);
        }
    }

    public async Task<StartSessionResult> StartSessionAsync(CancellationToken cancellationToken)
    {
        var session = Session ?? GetPersistedSession();
        if (session == null)
        {
            return FinishStartSession(null);
        }

        _tempSession = session;
        _sessionEndedImplicitlyResponse = null;

        // API call might refresh session
        var scopesResponse = await _authenticationApiClient.GetScopesAsync(cancellationToken).Safe().ConfigureAwait(false);
        if (scopesResponse.Succeeded)
        {
            _tempSession = _tempSession with { Scopes = scopesResponse.Scopes };
        }
        else if (scopesResponse.Code == ResponseCode.Unauthorized)
        {
            _tempSession = null;
        }

        return FinishStartSession(scopesResponse);
    }

    public async Task<StartSessionResult> StartSessionAsync(NetworkCredential credential, CancellationToken cancellationToken)
    {
        _sessionEndedImplicitlyResponse = null;
        await EndSessionAsync().ConfigureAwait(false);

        var authRequestData = new AuthInfoRequest { Username = credential.UserName };
        var authInfo = await _authenticationApiClient.GetAuthInfoAsync(authRequestData, cancellationToken).Safe().ConfigureAwait(false);
        if (!authInfo.Succeeded)
        {
            return StartSessionResult.Failure(StartSessionResultCode.SignInRequired, authInfo);
        }

        var srpClient = _srpClientFactory.Create(credential, authInfo);
        var srpClientHandshake = srpClient.ComputeHandshake(Convert.FromBase64String(authInfo.ServerEphemeral), ServerEphemeralLength);
        try
        {
            var authData = new AuthRequest
            {
                ClientEphemeral = Convert.ToBase64String(srpClientHandshake.Ephemeral),
                ClientProof = Convert.ToBase64String(srpClientHandshake.Proof),
                SrpSession = authInfo.SrpSession,
                Username = credential.UserName,
            };

            var response = await _authenticationApiClient.LoginAsync(authData, cancellationToken).Safe().ConfigureAwait(false);
            if (!response.Succeeded)
            {
                return StartSessionResult.Failure(StartSessionResultCode.SignInRequired, response);
            }

            if (!srpClientHandshake.TryComputeSharedKey(Convert.FromBase64String(response.ServerProof)))
            {
                _logger.LogWarning("Invalid server proof");

                return StartSessionResult.Failure(StartSessionResultCode.SignInRequired, new ApiResponse { Code = ResponseCode.SrpError });
            }

            _tempSession = new Session
            {
                Id = response.SessionId,
                UserId = response.UserId,
                AccessToken = response.AccessToken,
                RefreshToken = response.RefreshToken,
                Scopes = response.Scopes,
                MultiFactorEnabled = (response.MultipleFactor?.Methods ?? default) != default,
                MultiFactor = response.MultipleFactor,
                PasswordMode = response.PasswordMode,
            };

            if (_tempSession.PasswordMode != PasswordMode.Dual)
            {
                if (!_tempSession.MultiFactorEnabled)
                {
                    return await UnlockDataAsync(credential.SecurePassword, cancellationToken).ConfigureAwait(false);
                }

                _password = credential.SecurePassword;
            }
        }
        catch (SrpException ex)
        {
            _logger.LogError("SRP calculation failed: {Error}", ex.CombinedMessage());

            return StartSessionResult.Failure(StartSessionResultCode.SignInRequired, new ApiResponse { Code = ResponseCode.SrpError });
        }

        return FinishStartSession(null);
    }

    public Task<StartSessionResult> AuthenticateWithTotpAsync(string totp, CancellationToken cancellationToken)
    {
        var session = Session;
        if (session == null)
        {
            return Task.FromResult(StartSessionResult.Failure(StartSessionResultCode.SignInRequired));
        }

        var authRequest = new MultiFactorAuthenticationRequest { Totp = totp };

        return FinishTwoFactorAuthenticationAsync(authRequest, cancellationToken);
    }

    public Task<StartSessionResult> AuthenticateWithFido2Async(Fido2AssertionResult fido2Response, CancellationToken cancellationToken)
    {
        var authRequest = new MultiFactorAuthenticationRequest
        {
            Fido2Response = new Fido2Response
            {
                AuthenticationOptions = fido2Response.AuthenticationOptions,
                ClientData = fido2Response.ClientData,
                AuthenticatorData = fido2Response.AuthenticatorData,
                Signature = fido2Response.Signature,
                CredentialId = fido2Response.CredentialId.ToList(),
            },
        };

        return FinishTwoFactorAuthenticationAsync(authRequest, cancellationToken);
    }

    public async Task<StartSessionResult> UnlockDataAsync(SecureString dataPassword, CancellationToken cancellationToken)
    {
        var session = Session;
        if (session == null)
        {
            return StartSessionResult.Failure(StartSessionResultCode.SignInRequired);
        }

        try
        {
            _addressKeyProvider.ClearUserAddressesCache();

            await _keyPassphraseProvider.CalculatePassphrasesAsync(dataPassword, cancellationToken).ConfigureAwait(false);

            var address = await _addressKeyProvider.GetUserDefaultAddressAsync(cancellationToken).ConfigureAwait(false);

            if (!address.GetPrimaryKey().PrivateKeyIsUnlocked)
            {
                _keyPassphraseProvider.ClearPassphrases();

                var failureResult = _tempSession?.PasswordMode == PasswordMode.Dual
                    ? StartSessionResult.Failure(
                        StartSessionResultCode.DataPasswordRequired,
                        new ApiResponse
                        {
                            Code = ResponseCode.IncorrectLoginCredentials,
                            Error = "Incorrect mailbox password. Please try again",
                        })
                    : StartSessionResult.Failure(
                        StartSessionResultCode.Failure,
                        new ApiResponse
                        {
                            Code = ResponseCode.IncorrectLoginCredentials,
                            Error = "Could not decrypt primary address key",
                        });

                return failureResult;
            }
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            await EndSessionAsync().ConfigureAwait(false);

            return StartSessionResult.Failure(
                StartSessionResultCode.Failure,
                new ApiResponse
                {
                    Code = ResponseCode.Unknown,
                    Error = (ex is ApiException apiException) ? apiException.Message : null,
                });
        }

        return FinishStartSession(null);
    }

    public Task EndSessionAsync()
    {
        return _endSession.RunAsync();
    }

    public async Task<(Session Session, Func<CancellationToken, Task<Session?>> GetRefreshedSessionAsync)?> GetSessionAsync(CancellationToken cancellationToken)
    {
        await _refreshSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var currentRefreshTask = _refreshTask;
            var session = Session;

            return session is not null ? (session, ct => GetRefreshedSessionAsync(session, currentRefreshTask, ct)) : null;
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }

    public Task EndSessionAsync(string sessionId, ApiResponse apiResponse)
    {
        if (Session?.Id != sessionId)
        {
            return Task.CompletedTask;
        }

        _sessionEndedImplicitlyResponse = apiResponse;
        return EndSessionAsync();
    }

    private async Task<Session?> GetRefreshedSessionAsync(Session session, Lazy<Task> previousRefreshTask, CancellationToken cancellationToken)
    {
        var newRefreshTask = new Lazy<Task>(() => RefreshSessionAsync(session, cancellationToken));

        await _refreshSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Interlocked.CompareExchange(ref _refreshTask, newRefreshTask, previousRefreshTask);

            await _refreshTask.Value.WaitAsync(cancellationToken).ConfigureAwait(false);

            return Session;
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }

    private async Task RefreshSessionAsync(Session? session, CancellationToken cancellationToken)
    {
        if (session is null)
        {
            throw new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized);
        }

        _logger.LogInformation(
            "Refreshing token of session with ID=\"{SessionId}\" " +
            "(expired access token \"{AccessTokenFingerprint:x4}\", refresh token \"{RefreshTokenFingerprint:x4}\")",
            session.Id,
            session.AccessToken.GetHashCode() & 0xFFFF,
            session.RefreshToken.GetHashCode() & 0xFFFF);

        RefreshSessionResponse response;
        try
        {
            response = await _authenticationApiClient.RefreshSessionAsync(
                new RefreshSessionParameters(session.RefreshToken),
                session.Id,
                session.AccessToken,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Refit.ApiException e) when (e.StatusCode is
                                               >= HttpStatusCode.BadRequest and
                                               < HttpStatusCode.InternalServerError and
                                               not HttpStatusCode.RequestTimeout and
                                               not HttpStatusCode.TooManyRequests)
        {
            var apiResponse = await e.TryGetContentAsApiResponseAsync<ApiResponse>().ConfigureAwait(false);

            await EndSessionAsync(session.Id, apiResponse ?? new ApiResponse { Code = ResponseCode.SessionRefreshFailed }).ConfigureAwait(false);

            throw;
        }

        _logger.LogInformation(
            "Refreshing token of session with ID=\"{SessionId}\" succeeded " +
            "(new access token \"{AccessTokenFingerprint:x4}\", refresh token \"{RefreshTokenFingerprint:x4}\", refresh counter: {RefreshCounter})",
            session.Id,
            response.AccessToken.GetHashCode() & 0xFFFF,
            response.RefreshToken.GetHashCode() & 0xFFFF,
            response.RefreshCounter);

        session = session with
        {
            /* Session ID value does not change when refreshing tokens */
            Id = response.Uid,
            AccessToken = response.AccessToken,
            RefreshToken = response.RefreshToken,
            Scopes = response.Scopes,
        };

        _sessionRepository.Set(session);

        if (_tempSession != null)
        {
            _tempSession = session;
        }

        if (_session != null)
        {
            _session = session;
        }
    }

    private async Task<StartSessionResult> FinishTwoFactorAuthenticationAsync(MultiFactorAuthenticationRequest authRequest, CancellationToken cancellationToken)
    {
        var session = Session;
        if (session == null)
        {
            return StartSessionResult.Failure(StartSessionResultCode.SignInRequired);
        }

        var response = await _authenticationApiClient.LoginAsync(authRequest, cancellationToken).Safe().ConfigureAwait(false);
        if (!response.Succeeded)
        {
            // Failed FIDO2 authentication attempt resets the session.
            // Failed TOTP authentication attempt does not reset the session and can be retried.
            if (response.Code == ResponseCode.IncorrectLoginCredentials && authRequest.Totp is not null && session.MultiFactor is not null)
            {
                return StartSessionResult.SecondFactorAuthenticationRequired(session.MultiFactor, response);
            }

            await EndSessionAsync().ConfigureAwait(false);

            return StartSessionResult.Failure(StartSessionResultCode.SignInRequired, response);
        }

        session = session with { Scopes = response.Scopes, MultiFactorEnabled = false };
        _tempSession = session;

        if (_tempSession.PasswordMode != PasswordMode.Dual && _password != null)
        {
            return await UnlockDataAsync(_password, cancellationToken).ConfigureAwait(false);
        }

        return FinishStartSession(null);
    }

    private StartSessionResult FinishStartSession(ApiResponse? response)
    {
        var session = _tempSession;
        if (session == null)
        {
            Session = null;

            return StartSessionResult.Failure(
                StartSessionResultCode.SignInRequired,
                response ?? _sessionEndedImplicitlyResponse);
        }

        if (response?.Succeeded == false)
        {
            return StartSessionResult.Failure(StartSessionResultCode.Failure, response);
        }

        if (session.MultiFactorEnabled && !session.Scopes.Contains("full"))
        {
            return StartSessionResult.SecondFactorAuthenticationRequired(session.MultiFactor);
        }

        if (session.PasswordMode == PasswordMode.Dual && !_keyPassphraseProvider.ContainsAtLeastOnePassphrase)
        {
            return StartSessionResult.Failure(StartSessionResultCode.DataPasswordRequired);
        }

        // Clearing the cache once again in case something was added to it while signing out.
        _cache.Clear();

        _password = null;

        session = _tempSession;

        if (session != null && _userClient.GetCachedUser() is { } user && user.Id == session.UserId)
        {
            session = session with
            {
                Username = user.Name,
                UserEmailAddress = user.EmailAddress,
            };
        }

        Session = session;

        if (session == null)
        {
            return StartSessionResult.Failure(StartSessionResultCode.SignInRequired, response);
        }

        var result = StartSessionResult.Success(session);
        OnSessionStarted(result);

        return result;
    }

    private Task EndSessionInternalAsync(CancellationToken cancellationToken)
    {
        return WithLoggedException(UnsafeEndSessionInternalAsync);

        async Task UnsafeEndSessionInternalAsync()
        {
            if ((Session ?? GetPersistedSession()) == null)
            {
                return;
            }

            var session = _session;

            _cache.Clear();
            _keyPassphraseProvider.ClearPassphrases();
            _userClient.ClearCache();

            try
            {
                if (_sessionEndedImplicitlyResponse is null && session is not null)
                {
                    await _authenticationApiClient.LogoutAsync(session.Id, session.AccessToken, cancellationToken).Safe().ConfigureAwait(false);
                }
            }
            finally
            {
                // Abandon session even if logoff has failed
                Session = null;
            }

            if (_sessionEndedImplicitlyResponse != null && session != null)
            {
                OnSessionEnded(_sessionEndedImplicitlyResponse);
            }
        }
    }

    private void OnSessionStarted(StartSessionResult result)
    {
        SessionStarted?.Invoke(this, result);
    }

    private void OnSessionEnded(ApiResponse reason)
    {
        SessionEndedImplicitly?.Invoke(this, reason);
    }

    private Session? GetPersistedSession()
    {
        return _sessionRepository.Get();
    }

    private Task WithLoggedException(Func<Task> origin)
    {
        return _logger.WithLoggedException(origin, includeStackTrace: true);
    }
}
