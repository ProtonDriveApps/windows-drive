using System.Net;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Proton.Drive.Sdk.Sync.Client.Configuration;
using Proton.Drive.Shared.Client;
using Proton.Drive.Shared.Net.Http;
using Proton.Drive.Shared.Offline;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Client.Offline;

internal sealed class OfflineService : IOfflineService, IOfflinePolicyProvider, IDisposable
{
    private static readonly TimeSpan TimerDelayAfterSwitchingOnline = TimeSpan.FromSeconds(2);

    private readonly IMessenger _messenger;
    private readonly TooManyRequestsBlockedEndpoints _blockedEndpoints;
    private readonly Lazy<IEnumerable<IOfflineStateAware>> _offlineStateAware;
    private readonly ILogger<OfflineService> _logger;

    private readonly CircuitBreakerManualControl _manualControl = new();
    private readonly CircuitBreakerStateProvider _stateProvider = new();
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencyPipeline;

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

        var circuitBreakerOptions = new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<Exception>(IsNotOperationCanceledException)
                .HandleResult(IsWorthBreaking),
            SamplingDuration = TimeSpan.FromMinutes(7),
            MinimumThroughput = 20,
            FailureRatio = 0.7,
            BreakDuration = config.DelayBeforeSwitchingOnline,
            ManualControl = _manualControl,
            OnOpened = OnOpened,
            OnClosed = OnClosed,
            OnHalfOpened = OnHalfOpen,
        };

        var fallbackOptions = new FallbackStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>().HandleResult(IsClientError),
            FallbackAction = FallbackAction,
        };

        _resiliencyPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddCircuitBreaker(circuitBreakerOptions)
            .AddFallback(fallbackOptions)
            .Build();

        _timer = scheduler.CreateTimer();
        _timer.Interval = config.DelayBeforeSwitchingOnline + TimerDelayAfterSwitchingOnline;
        _timer.Tick += OnTimerTick;
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

        if (_stateProvider.CircuitState == CircuitState.Open)
        {
            _logger.LogInformation("Resetting the offline state");
            _ = _manualControl.CloseAsync();
        }
    }

    public ResiliencePipeline<HttpResponseMessage> GetPolicy() => _resiliencyPipeline;

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

    private static bool IsAppUpdateRequired(ApiResponse? apiResponse)
    {
        return apiResponse is
        {
            Code: ResponseCode.InvalidApp or ResponseCode.OutdatedApp,
        };
    }

    private async ValueTask<Outcome<HttpResponseMessage>> FallbackAction(FallbackActionArguments<HttpResponseMessage> args)
    {
        var response = args.Outcome.Result;
        if (response == null)
        {
            return args.Outcome;
        }

        var apiResponse = await response.TryReadFromJsonAsync<ApiResponse?>(CancellationToken.None).ConfigureAwait(false);

        if (IsAppUpdateRequired(apiResponse))
        {
            HandleAppUpdateRequired();
        }

        return args.Outcome;
    }

    private ValueTask OnOpened(OnCircuitOpenedArguments<HttpResponseMessage> args)
    {
        _logger.LogInformation("Service offline: API requests cannot flow.");

        _timer.Start();
        OnStateChanged(OfflineStatus.Offline);

        return ValueTask.CompletedTask;
    }

    private ValueTask OnHalfOpen(OnCircuitHalfOpenedArguments args)
    {
        _logger.LogInformation("Service offline: Single API request can flow to see if it is online");

        _timer.Stop();
        OnStateChanged(OfflineStatus.Testing);

        return ValueTask.CompletedTask;
    }

    private ValueTask OnClosed(OnCircuitClosedArguments<HttpResponseMessage> args)
    {
        _logger.LogInformation("Service online: API requests flow normally.");

        _timer.Stop();
        OnStateChanged(OfflineStatus.Online);

        return ValueTask.CompletedTask;
    }

    private void OnStateChanged(OfflineStatus status)
    {
        foreach (var listener in _offlineStateAware.Value)
        {
            listener.OnOfflineStateChanged(status);
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        /* The Polly circuit breaker doesn't have a timer inside. It handles its state
         * when the request passes through or when its state is requested. To raise OnHalfOpen
         * event in a timely fashion, we are requesting policy state soon after the circuit
         * should have been switched to half-open state. */
        _ = _stateProvider.CircuitState;
    }

    private void HandleAppUpdateRequired()
    {
        _manualControl.IsolateAsync();

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
