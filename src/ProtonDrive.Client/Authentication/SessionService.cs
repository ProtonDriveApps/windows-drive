using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proton.Security;
using ProtonDrive.Client.Authentication.Contracts;
using ProtonDrive.Client.Cryptography;
using ProtonDrive.Shared.Caching;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.Client.Authentication;

internal sealed class SessionService : ISessionService, ISessionProvider
{
    private readonly IAuthenticationApiClient _authenticationApiClient;
    private readonly ISrpClient _srpClient;
    private readonly IUserClient _userClient;
    private readonly IAddressKeyProvider _addressKeyProvider;
    private readonly IKeyPassphraseProvider _keyPassphraseProvider;
    private readonly ICryptographyService _cryptographyService;
    private readonly IProtectedRepository<Session> _sessionRepository;
    private readonly IClearableMemoryCache _cache;
    private readonly ILogger<SessionService> _logger;

    private readonly SingleAction _endSession;
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);

    private SecureString? _password;
    private Session? _tempSession;
    private Session? _session;
    private Lazy<Task> _refreshTask = new(() => Task.CompletedTask);
    private ApiResponse? _sessionEndedImplicitlyResponse;

    public SessionService(
        IAuthenticationApiClient authenticationApiClient,
        ISrpClient srpClient,
        IUserClient userClient,
        IAddressKeyProvider addressKeyProvider,
        IKeyPassphraseProvider keyPassphraseProvider,
        ICryptographyService cryptographyService,
        IProtectedRepository<Session> sessionRepository,
        IClearableMemoryCache cache,
        ILogger<SessionService> logger)
    {
        _authenticationApiClient = authenticationApiClient;
        _srpClient = srpClient;
        _userClient = userClient;
        _addressKeyProvider = addressKeyProvider;
        _keyPassphraseProvider = keyPassphraseProvider;
        _cryptographyService = cryptographyService;
        _sessionRepository = sessionRepository;
        _cache = cache;
        _logger = logger;

        _endSession = new SingleAction(EndSessionInternalAsync);
    }

    /// <summary>
    /// Session ended implicitly.
    /// </summary>
    public event EventHandler<ApiResponse>? SessionEnded;

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

        var srpServerGeneratedChallenge = new SrpServerGeneratedChallenge(Version: 4, Convert.FromBase64String(authInfo.ServerEphemeral ?? string.Empty));

        try
        {
            var srpClientResponse = _srpClient.CalculateResponse(
                srpServerGeneratedChallenge,
                Convert.FromBase64String(authInfo.Salt ?? string.Empty),
                authInfo.Modulus ?? string.Empty,
                credential.UserName,
                credential.SecurePassword);

            var authData = new AuthRequest
            {
                ClientEphemeral = Convert.ToBase64String(srpClientResponse.ClientGeneratedChallenge.Ephemeral),
                ClientProof = Convert.ToBase64String(srpClientResponse.ClientProof),
                SrpSession = authInfo.SrpSession,
                Username = credential.UserName,
            };

            var response = await _authenticationApiClient.LoginAsync(authData, cancellationToken).Safe().ConfigureAwait(false);
            if (!response.Succeeded)
            {
                return StartSessionResult.Failure(StartSessionResultCode.SignInRequired, response);
            }

            if (!Convert.ToBase64String(srpClientResponse.ClientGeneratedChallenge.ExpectedProof).Equals(response.ServerProof))
            {
                _logger.LogWarning("Invalid server proof");

                return StartSessionResult.Failure(StartSessionResultCode.SignInRequired, new ApiResponse { Code = ResponseCode.SrpError });
            }

            _tempSession = new Session
            {
                Id = response.Uid,
                AccessToken = response.AccessToken,
                RefreshToken = response.RefreshToken,
                Scopes = response.Scopes,
                TwoFactorEnabled = (response.TwoFactor?.Enabled ?? 0) != 0,
                PasswordMode = response.PasswordMode,
                UserId = response.UserId,
            };

            if (_tempSession.PasswordMode != PasswordMode.Dual)
            {
                if (!_tempSession.TwoFactorEnabled)
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

    public async Task<StartSessionResult> FinishTwoFactorAuthenticationAsync(string secondFactor, CancellationToken cancellationToken)
    {
        var session = Session;
        if (session == null)
        {
            return StartSessionResult.Failure(StartSessionResultCode.SignInRequired);
        }

        var authRequestData = new AuthSecondFactorRequest { TwoFactorCode = secondFactor };
        var response = await _authenticationApiClient.LoginAsync(authRequestData, cancellationToken).Safe().ConfigureAwait(false);
        if (!response.Succeeded)
        {
            if (response.Code == ResponseCode.IncorrectLoginCredentials)
            {
                return StartSessionResult.Failure(StartSessionResultCode.SecondFactorCodeRequired, response);
            }

            await EndSessionAsync().ConfigureAwait(false);

            return StartSessionResult.Failure(StartSessionResultCode.SignInRequired, response);
        }

        session = session with { Scopes = response.Scopes, TwoFactorEnabled = false };
        _tempSession = session;

        if (_tempSession.PasswordMode != PasswordMode.Dual && _password != null)
        {
            return await UnlockDataAsync(_password, cancellationToken).ConfigureAwait(false);
        }

        return FinishStartSession(null);
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

            var passphraseCanUnlock = _cryptographyService.PrivateKeyIsValid(address.GetPrimaryKey().PrivateKey);

            if (!passphraseCanUnlock)
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
                    Error = (ex is ApiException apiException) ? apiException.Message : default,
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

            return session is not null ? (session, ct => GetRefreshedSessionAsync(session, currentRefreshTask, ct)) : default;
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

        _logger.LogInformation("Refreshing session token");

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
            var apiResponse = await e.GetContentAsApiResponseAsync<ApiResponse>().ConfigureAwait(false);

            await EndSessionAsync(session.Id, apiResponse ?? new ApiResponse { Code = ResponseCode.SessionRefreshFailed }).ConfigureAwait(false);

            throw;
        }

        _logger.LogInformation("Refreshing session token succeeded");

        session = session with
        {
            /* Id value does not change when refreshing tokens */
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

        if (session.TwoFactorEnabled && !session.Scopes.Contains("full"))
        {
            return StartSessionResult.Failure(StartSessionResultCode.SecondFactorCodeRequired);
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

        return session == null
            ? StartSessionResult.Failure(StartSessionResultCode.SignInRequired, response)
            : StartSessionResult.Success(session);
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

    private void OnSessionEnded(ApiResponse reason)
    {
        SessionEnded?.Invoke(this, reason);
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
