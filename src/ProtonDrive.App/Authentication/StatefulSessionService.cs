using System.Net;
using System.Security;
using DotNext.Threading;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Account;
using ProtonDrive.App.Services;
using ProtonDrive.App.Volumes;
using ProtonDrive.Client;
using ProtonDrive.Client.Authentication;
using ProtonDrive.Shared.Authentication;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Offline;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Authentication;

public class StatefulSessionService
    : IStatefulSessionService, IAuthenticationService, IStartableService, IStoppableService, IAccountStateAware, IMainVolumeStateAware
{
    private readonly Client.Authentication.IAuthenticationService _clientAuthenticationService;
    private readonly IFido2Authenticator _fido2Authenticator;
    private readonly IOfflineService _offlineService;
    private readonly Lazy<IEnumerable<ISessionStateAware>> _sessionStateAware;
    private readonly ILogger<StatefulSessionService> _logger;

    private readonly CancellationHandle _cancellationHandle = new();
    private readonly SerialScheduler _scheduler = new();
    private readonly AsyncManualResetEvent _accountSetupCompleted = new(initialState: false);

    private volatile bool _stopping;
    private bool _isFirstSessionStart = true;
    private SessionState _state = SessionState.None;
    private Fido2AssertionParameters? _fido2AssertionParameters;
    private string? _accountSetupErrorMessage;

    public StatefulSessionService(
        Client.Authentication.IAuthenticationService clientAuthenticationService,
        IFido2Authenticator fido2Authenticator,
        IOfflineService offlineService,
        Lazy<IEnumerable<ISessionStateAware>> sessionStateAware,
        ILogger<StatefulSessionService> logger)
    {
        _clientAuthenticationService = clientAuthenticationService;
        _fido2Authenticator = fido2Authenticator;
        _offlineService = offlineService;
        _sessionStateAware = sessionStateAware;
        _logger = logger;

        _clientAuthenticationService.SessionEndedImplicitly += OnClientAuthenticationServiceSessionEnded;
    }

    private SessionState State
    {
        get => _state;
        set
        {
            if (value.Status != _state.Status ||
                value.SigningInStatus != _state.SigningInStatus)
            {
                _state = value;
                OnStateChanged(value);
            }

            if (value.Status is not SessionStatus.NotStarted and not SessionStatus.Starting and not SessionStatus.SigningIn)
            {
                _isFirstSessionStart = false;
            }
        }
    }

    void IAccountStateAware.OnAccountStateChanged(AccountState value)
    {
        if (value.Status is AccountStatus.Failed)
        {
            _accountSetupErrorMessage = "Something went wrong";
            _accountSetupCompleted.Set();
        }
        else
        {
            _accountSetupErrorMessage = null;
            _accountSetupCompleted.Reset();
        }
    }

    void IMainVolumeStateAware.OnMainVolumeStateChanged(VolumeState value)
    {
        switch (value.Status)
        {
            case VolumeStatus.Ready:
                _accountSetupErrorMessage = null;
                _accountSetupCompleted.Set();
                break;

            case VolumeStatus.Failed:
                _accountSetupErrorMessage = value.ErrorMessage;
                _accountSetupCompleted.Set();
                break;

            default:
                _accountSetupErrorMessage = null;
                _accountSetupCompleted.Reset();
                break;
        }
    }

    Task IStartableService.StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{nameof(StatefulSessionService)} is starting, scheduling session start");
        StartSessionAsync();

        return Task.CompletedTask;
    }

    async Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{nameof(StatefulSessionService)} is stopping");
        _stopping = true;
        _cancellationHandle.Cancel();

        // Wait for the scheduler to finish all previously scheduled tasks
        await _scheduler.Schedule(() => false, CancellationToken.None).ConfigureAwait(false);

        _logger.LogInformation($"{nameof(StatefulSessionService)} stopped");
    }

    public Task StartSessionAsync()
    {
        ForceOnline();
        return Schedule(InternalStartSessionAsync);
    }

    public Task EndSessionAsync()
    {
        _logger.LogInformation("Requested to end session (sign-out)");

        return ScheduleEndSession(ApiResponse.Success);
    }

    public Task AuthenticateAsync(NetworkCredential credential)
    {
        ForceOnline();
        return Schedule(ct => InternalAuthenticateAsync(credential, ct));
    }

    public Task AuthenticateWithTotpAsync(string totp)
    {
        ForceOnline();
        return Schedule(ct => InternalAuthenticateWithTotpAsync(totp, ct));
    }

    public Task AuthenticateWithFido2Async()
    {
        ForceOnline();
        return Schedule(InternalAuthenticateWithFido2Async);
    }

    public Task FinishTwoPasswordAuthenticationAsync(SecureString secondPassword)
    {
        ForceOnline();
        return Schedule(ct => InternalFinishTwoPasswordAuthenticationAsync(secondPassword, ct));
    }

    public Task CancelAuthenticationAsync()
    {
        _logger.LogInformation("Requested to cancel session authentication");

        if (State.Status is not SessionStatus.SigningIn && State.SigningInStatus is SigningInStatus.None)
        {
            return Task.CompletedTask;
        }

        return ScheduleEndSession();
    }

    public void RestartAuthentication()
    {
        if (State.SigningInStatus is
            SigningInStatus.WaitingForSecondFactorAuthentication
            or SigningInStatus.WaitingForDataPassword
            or SigningInStatus.WaitingForAuthenticationPassword)
        {
            SetSigningIn(SigningInStatus.WaitingForAuthenticationPassword);
        }
    }

    private static MultiFactorAuthenticationMethods GetMultiFactorMethods(StartSessionResult? result)
    {
        var methods = (MultiFactorAuthenticationMethods)(result?.MultiFactor?.Methods ?? default);

        methods &= MultiFactorAuthenticationMethods.Totp | MultiFactorAuthenticationMethods.Fido2;

        if (result?.MultiFactor?.Fido2 is null)
        {
            methods &= ~MultiFactorAuthenticationMethods.Fido2;
        }

        return methods;
    }

    private async Task InternalStartSessionAsync(CancellationToken cancellationToken)
    {
        if (_stopping || State.Status is not SessionStatus.NotStarted and not SessionStatus.Failed)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        SetState(SessionStatus.Starting);

        var result = await _clientAuthenticationService.StartSessionAsync(cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        SetState(result);
    }

    private async Task InternalAuthenticateAsync(NetworkCredential credential, CancellationToken cancellationToken)
    {
        if (_stopping)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        SetSigningIn(SigningInStatus.Authenticating);

        var result = await _clientAuthenticationService.StartSessionAsync(credential, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (!result.IsSuccess)
        {
            SetState(result);
            return;
        }

        SetState(SessionStatus.Started, result, SigningInStatus.Authenticating);

        SetState(await GetAccountSetupResultAsync(cancellationToken).ConfigureAwait(false) ?? result);
    }

    private async Task InternalAuthenticateWithTotpAsync(string totp, CancellationToken cancellationToken)
    {
        if (_stopping ||
            State.Status != SessionStatus.SigningIn ||
            State.SigningInStatus != SigningInStatus.WaitingForSecondFactorAuthentication ||
            !State.MultiFactorAuthenticationMethods.HasFlag(MultiFactorAuthenticationMethods.Totp))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        SetSigningIn(SigningInStatus.Authenticating);

        var result = await _clientAuthenticationService.AuthenticateWithTotpAsync(totp, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (!result.IsSuccess)
        {
            SetState(result);
            return;
        }

        SetState(SessionStatus.Started, result, SigningInStatus.Authenticating);

        SetState(await GetAccountSetupResultAsync(cancellationToken).ConfigureAwait(false) ?? result);
    }

    private async Task InternalAuthenticateWithFido2Async(CancellationToken cancellationToken)
    {
        var fido2AssertionParameters = _fido2AssertionParameters;

        if (_stopping ||
            State.Status != SessionStatus.SigningIn ||
            State.SigningInStatus != SigningInStatus.WaitingForSecondFactorAuthentication ||
            !State.MultiFactorAuthenticationMethods.HasFlag(MultiFactorAuthenticationMethods.Fido2) ||
            fido2AssertionParameters is null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        SetSigningIn(SigningInStatus.Authenticating);

        var (assertionResult, errorResponse) = await AssertFido2Async(fido2AssertionParameters, cancellationToken).ConfigureAwait(false);

        if (assertionResult is null)
        {
            await InternalEndSessionAsync(errorResponse).ConfigureAwait(false);
            return;
        }

        var result = await _clientAuthenticationService.AuthenticateWithFido2Async(assertionResult, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (!result.IsSuccess)
        {
            SetState(result);
            return;
        }

        SetState(SessionStatus.Started, result, SigningInStatus.Authenticating);

        SetState(await GetAccountSetupResultAsync(cancellationToken).ConfigureAwait(false) ?? result);
    }

    private async Task<(Fido2AssertionResult? Result, ApiResponse? ErrorResponse)> AssertFido2Async(Fido2AssertionParameters fido2AssertionParameters, CancellationToken cancellationToken)
    {
        try
        {
            return (await _fido2Authenticator.AssertAsync(fido2AssertionParameters, cancellationToken).ConfigureAwait(false), null);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("FIDO2 operation cancelled");
            return (Result: null, ErrorResponse: ApiResponse.Success);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning("FIDO2 is not supported: {ErrorMessage}", ex.Message);
            return (Result: null, ErrorResponse: new ApiResponse { Code = ResponseCode.Unknown, Error = ex.Message });
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("FIDO2 operation timed out");
            return (Result: null, ErrorResponse: new ApiResponse { Code = ResponseCode.Unknown, Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError("FIDO2 operation failed: {ErrorCode} : {ErrorMessage}", ex.GetRelevantFormattedErrorCode(), ex.Message);
            return (Result: null, ErrorResponse: new ApiResponse { Code = ResponseCode.Unknown, Error = ex.Message });
        }
    }

    private async Task InternalFinishTwoPasswordAuthenticationAsync(SecureString secondPassword, CancellationToken cancellationToken)
    {
        if (_stopping || State.Status != SessionStatus.SigningIn || State.SigningInStatus != SigningInStatus.WaitingForDataPassword)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        SetSigningIn(SigningInStatus.Authenticating);

        var result = await _clientAuthenticationService.UnlockDataAsync(secondPassword, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (!result.IsSuccess)
        {
            SetState(result);
            return;
        }

        SetState(SessionStatus.Started, result, SigningInStatus.Authenticating);

        SetState(await GetAccountSetupResultAsync(cancellationToken).ConfigureAwait(false) ?? result);
    }

    private async Task<StartSessionResult?> GetAccountSetupResultAsync(CancellationToken cancellationToken)
    {
        await _accountSetupCompleted.WaitAsync(cancellationToken).ConfigureAwait(false);

        var errorMessage = _accountSetupErrorMessage;

        if (string.IsNullOrEmpty(errorMessage))
        {
            // Account related setup has succeeded
            return null;
        }

        // Account related setup has failed
        return StartSessionResult.Failure(
            StartSessionResultCode.Failure,
            new ApiResponse
            {
                Code = ResponseCode.Unknown,
                Error = errorMessage,
            });
    }

    private Task ScheduleEndSession(ApiResponse? reason = null)
    {
        _cancellationHandle.Cancel();
        ForceOnline();
        return Schedule(_ => InternalEndSessionAsync(reason));
    }

    private async Task InternalEndSessionAsync(ApiResponse? reason)
    {
        if (_stopping || State.Status is SessionStatus.NotStarted)
        {
            return;
        }

        SetState(SessionStatus.Ending);
        SetState(SessionStatus.NotStarted);

        if (reason is not null)
        {
            SetState(StartSessionResult.Failure(StartSessionResultCode.Failure, reason));
        }

        await _clientAuthenticationService.EndSessionAsync().ConfigureAwait(false);
    }

    private void OnClientAuthenticationServiceSessionEnded(object? sender, ApiResponse reason)
    {
        _logger.LogWarning("Session terminated by the backend");

        ScheduleEndSession(reason);
    }

    private void SetState(SessionStatus status)
    {
        State = new SessionState
        {
            Status = status,
            IsFirstSessionStart = _isFirstSessionStart,
        };
    }

    private void SetState(StartSessionResult result)
    {
        if (result.IsSuccess)
        {
            SetState(SessionStatus.Started, result);
            return;
        }

        if (result.Code == StartSessionResultCode.Failure && State.Status is SessionStatus.Starting)
        {
            SetState(SessionStatus.Failed, result);
            return;
        }

        switch (result.Code)
        {
            case StartSessionResultCode.SignInRequired:
            case StartSessionResultCode.Failure:
                SetState(SessionStatus.SigningIn, result, SigningInStatus.WaitingForAuthenticationPassword);
                break;

            case StartSessionResultCode.SecondFactorRequired:
                SetSigningIn(SigningInStatus.WaitingForSecondFactorAuthentication, result);
                break;

            case StartSessionResultCode.DataPasswordRequired:
                SetSigningIn(SigningInStatus.WaitingForDataPassword, result);
                break;

            default:
                throw new NotSupportedException();
        }
    }

    private void SetState(SessionStatus status, StartSessionResult startSessionResult, SigningInStatus authenticationStatus = SigningInStatus.None)
    {
        State = new SessionState
        {
            Status = status,
            SigningInStatus = authenticationStatus,
            IsFirstSessionStart = _isFirstSessionStart,
            Response = startSessionResult.Response,
            Scopes = startSessionResult.Scopes,
            UserId = startSessionResult.UserId,
            Username = startSessionResult.Username,
            UserEmailAddress = startSessionResult.UserEmailAddress,
        };
    }

    private void SetSigningIn(SigningInStatus authenticationStatus, StartSessionResult? result = null)
    {
        State = new SessionState
        {
            Status = SessionStatus.SigningIn,
            SigningInStatus = authenticationStatus,
            IsFirstSessionStart = _isFirstSessionStart,
            UserId = result?.UserId,
            Response = result?.Response ?? ApiResponse.Success,
            MultiFactorAuthenticationMethods = GetMultiFactorMethods(result),
            IsFido2Available = _fido2Authenticator.IsAvailable,
        };

        _fido2AssertionParameters = result?.MultiFactor?.Fido2;
    }

    private void OnStateChanged(SessionState state)
    {
        LogStateChange(state);

        foreach (var listener in _sessionStateAware.Value)
        {
            listener.OnSessionStateChanged(state);
        }
    }

    private void LogStateChange(SessionState state)
    {
        if (!state.Response.Succeeded)
        {
            _logger.LogWarning("Session service responded with {ErrorCode}: {ErrorMessage}", state.Response.Code, state.Response.Error);
        }

        if (state.Status is SessionStatus.SigningIn || state.SigningInStatus is not SigningInStatus.None)
        {
            _logger.LogInformation("Session state changed to {SessionStatus}/{AuthenticationStatus}", state.Status, state.SigningInStatus);
        }
        else if (state.Status is SessionStatus.Started && state.SigningInStatus is not SigningInStatus.None)
        {
            _logger.LogInformation("Session state changed to {SessionStatus}/{AuthenticationStatus}, user with ID \"{UserId}\"", state.Status, state.SigningInStatus, state.UserId);
        }
        else if (!string.IsNullOrEmpty(state.UserId))
        {
            _logger.LogInformation("Session state changed to {SessionStatus}, user with ID \"{UserId}\"", state.Status, state.UserId);
        }
        else
        {
            _logger.LogInformation("Session state changed to {SessionStatus}", state.Status);
        }
    }

    private void ForceOnline()
    {
        if (_stopping)
        {
            return;
        }

        _offlineService.ForceOnline();
    }

    private Task Schedule(Func<CancellationToken, Task> action)
    {
        if (_stopping)
        {
            return Task.CompletedTask;
        }

        var cancellationToken = _cancellationHandle.Token;

        return _scheduler.Schedule(() => WithLoggedExceptions(() => WithSafeCancellation(() => action(cancellationToken))), cancellationToken);
    }

    private Task WithLoggedExceptions(Func<Task> origin)
    {
        return _logger.WithLoggedException(origin, "Authentication failed", includeStackTrace: true);
    }

    private async Task WithSafeCancellation(Func<Task> origin)
    {
        try
        {
            await origin().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
            _logger.LogInformation($"{nameof(StatefulSessionService)} operation was cancelled");
        }
    }
}
