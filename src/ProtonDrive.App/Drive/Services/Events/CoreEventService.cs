using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Services;
using ProtonDrive.Client;
using ProtonDrive.Client.Configuration;
using ProtonDrive.Client.Core.Events;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Drive.Services.Events;

internal sealed class CoreEventService : ISessionStateAware, IStoppableService, IDisposable
{
    private readonly ICoreEventClient _eventClient;
    private readonly ILogger<CoreEventService> _logger;

    private readonly SingleAction _getEvents;
    private readonly ISchedulerTimer _timer;

    private SessionStatus _sessionStatus = SessionStatus.NotStarted;
    private CoreEventResumeToken _resumeToken = CoreEventResumeToken.Start;
    private bool _stopping;

    public CoreEventService(
        DriveApiConfig apiConfig,
        ICoreEventClient eventClient,
        IScheduler scheduler,
        ILogger<CoreEventService> logger)
    {
        _eventClient = eventClient;
        _logger = logger;

        _getEvents = _logger.GetSingleActionWithExceptionsLoggingAndCancellationHandling(GetAllEvents, nameof(CoreEventService));

        _timer = scheduler.CreateTimer();
        _timer.Interval = apiConfig.EventsPollingInterval.RandomizedWithDeviation(0.2);
        _timer.Tick += TimerOnTick;
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        if (_sessionStatus == value.Status)
        {
            return;
        }

        _sessionStatus = value.Status;

        if (value.Status is SessionStatus.Started)
        {
            _getEvents.RunAsync();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            _getEvents.Cancel();
            ResetEventStream();
        }
    }

    async Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{nameof(CoreEventService)} is stopping");

        _stopping = true;
        _timer.Stop();
        _getEvents.Cancel();

        await WaitForCompletionAsync().ConfigureAwait(false);

        _logger.LogInformation($"{nameof(CoreEventService)} stopped");
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    internal Task WaitForCompletionAsync()
    {
        // Wait for the current scheduled task to complete
        return _getEvents.CurrentTask;
    }

    private void ResetEventStream()
    {
        Interlocked.Exchange(ref _resumeToken, CoreEventResumeToken.Start);
    }

    private void TimerOnTick(object? sender, EventArgs e)
    {
        _getEvents.RunAsync();
    }

    private async Task GetAllEvents(CancellationToken cancellationToken)
    {
        while (await GetEventsAsync(cancellationToken).ConfigureAwait(false))
        {
        }
    }

    private async Task<bool> GetEventsAsync(CancellationToken cancellationToken)
    {
        if (_stopping || _sessionStatus is not SessionStatus.Started)
        {
            return false;
        }

        try
        {
            var resumeToken = _resumeToken;

            var events = await _eventClient.GetEventsAsync(_resumeToken, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            var originalValue = Interlocked.CompareExchange(ref _resumeToken, events.ResumeToken, resumeToken);

            return originalValue == resumeToken && events.ResumeToken.HasMoreData;
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogWarning("Failed to get core events : {ErrorMessage}", ex.Message);

            return false;
        }
    }
}
