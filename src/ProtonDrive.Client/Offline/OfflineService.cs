using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using ProtonDrive.Client.Configuration;
using ProtonDrive.Shared.Net.Http;
using ProtonDrive.Shared.Offline;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.Client.Offline;

internal sealed class OfflineService : IOfflineService, IOfflinePolicyProvider, IDisposable
{
    private static readonly TimeSpan TimerDelayAfterSwitchingOnline = TimeSpan.FromSeconds(2);

    private readonly IMessenger _messenger;
    private readonly TooManyRequestsBlockedEndpoints _blockedEndpoints;
    private readonly Lazy<IEnumerable<IOfflineStateAware>> _offlineStateAware;
    private readonly ILogger<OfflineService> _logger;
    private readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> _circuitBreakerPolicy;
    private readonly AsyncPolicy<HttpResponseMessage> _offlinePolicy;

    private readonly ISchedulerTimer _timer;

    private volatile bool _appUpdateRequired;

    public OfflineService(
        DriveApiConfig config,
        IScheduler scheduler,
        IMessenger messenger,
        TooManyRequestsBlockedEndpoints blockedEndpoints,
        Lazy<IEnumerable<IOfflineStateAware>> offlineStateAware,
        ILogger<OfflineService> logger)
    {
        _messenger = messenger;
        _blockedEndpoints = blockedEndpoints;
        _offlineStateAware = offlineStateAware;
        _logger = logger;

        _circuitBreakerPolicy = Policy
            .Handle<Exception>(IsNotOperationCanceledException)
            .OrResult<HttpResponseMessage>(IsWorthBreaking)
            .CircuitBreakerAsync(
                config.ConsecutiveErrorsBeforeSwitchingOffline,
                config.DelayBeforeSwitchingOnline,
                OnBreak,
                OnReset,
                OnHalfOpen);

        _offlinePolicy = _circuitBreakerPolicy.WrapAsync(Policy
            .HandleResult<HttpResponseMessage>(IsClientError)
            .FallbackAsync(FallbackAction, (_, _) => Task.CompletedTask));

        _timer = scheduler.CreateTimer();
        _timer.Interval = config.DelayBeforeSwitchingOnline + TimerDelayAfterSwitchingOnline;
        _timer.Tick += TimerOnTick;
    }

    public void ForceOnline()
    {
        if (_appUpdateRequired)
        {
            _logger.LogWarning("Won't reset the offline state, the app update is required");
            return;
        }

        // Clearing HTTP endpoints blocked because of too many requests
        _blockedEndpoints.Clear();

        if (_circuitBreakerPolicy.CircuitState == CircuitState.Open)
        {
            _logger.LogInformation("Resetting the offline state");
            _circuitBreakerPolicy.Reset();
        }
    }

    public AsyncPolicy<HttpResponseMessage> GetPolicy() => _offlinePolicy;

    public void Dispose()
    {
        _timer.Dispose();
    }

    private static bool IsNotOperationCanceledException(Exception exception)
    {
        return exception is not OperationCanceledException;
    }

    private static bool IsWorthBreaking(HttpResponseMessage message)
    {
        return message.StatusCode switch
        {
            >= HttpStatusCode.OK and < HttpStatusCode.Ambiguous => false,   // 2xx
            HttpStatusCode.Unauthorized => false,                           // 401
            HttpStatusCode.Forbidden => false,                              // 403
            HttpStatusCode.NotFound => false,                               // 404
            HttpStatusCode.Conflict => false,                               // 409
            HttpStatusCode.UnprocessableEntity => false,                    // 422
            _ => true,
        };
    }

    private static bool IsClientError(HttpResponseMessage message)
    {
        return message.StatusCode is >= HttpStatusCode.BadRequest and <= HttpStatusCode.InternalServerError;
    }

    private async Task<HttpResponseMessage> FallbackAction(DelegateResult<HttpResponseMessage> result, Context context, CancellationToken cancellationToken)
    {
        var response = result.Result;
        var apiResponse = await response.TryReadFromJsonAsync(cancellationToken).ConfigureAwait(false);

        if (IsAppUpdateRequired(apiResponse))
        {
            HandleAppUpdateRequired();
        }

        return response;
    }

    private static bool IsAppUpdateRequired(ApiResponse? apiResponse)
    {
        return apiResponse is
        {
            Code: ResponseCode.InvalidApp or ResponseCode.OutdatedApp,
        };
    }

    private void OnBreak(DelegateResult<HttpResponseMessage> arg1, TimeSpan arg2)
    {
        _logger.LogInformation("Service offline: API requests cannot flow.");

        _timer.Start();
        OnStateChanged(OfflineStatus.Offline);
    }

    private void OnHalfOpen()
    {
        _logger.LogInformation("Service offline: Single API request can flow to see if it is online");

        _timer.Stop();
        OnStateChanged(OfflineStatus.Testing);
    }

    private void OnReset()
    {
        _logger.LogInformation("Service online: API requests flow normally.");

        _timer.Stop();
        OnStateChanged(OfflineStatus.Online);
    }

    private void OnStateChanged(OfflineStatus status)
    {
        foreach (var listener in _offlineStateAware.Value)
        {
            listener.OnOfflineStateChanged(status);
        }
    }

    private void TimerOnTick(object? sender, EventArgs e)
    {
        /* The Polly circuit breaker doesn't have a timer inside. It handles its state
         * when the request passes through or when its state is requested. To raise OnHalfOpen
         * event in a timely fashion, we are requesting policy state soon after the circuit
         * should have been switched to half-open state. */
        _ = _circuitBreakerPolicy.CircuitState;
    }

    private void HandleAppUpdateRequired()
    {
        _circuitBreakerPolicy.Isolate();

        if (!_appUpdateRequired)
        {
            _appUpdateRequired = true;
            OnAppUpdateRequired();
        }
    }

    private void OnAppUpdateRequired()
    {
        _messenger.Send<AppUpdateRequiredMessage>();
    }
}
