using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem;

internal sealed class DispatchingEventLogClientDecorator<TId> : IEventLogClient<TId>
{
    private readonly IEventLogClient<TId> _decoratedInstance;
    private readonly SingleAction _getEvents;

    private int _isEnabled;

    public DispatchingEventLogClientDecorator(IEventLogClient<TId> instanceToDecorate)
    {
        _decoratedInstance = instanceToDecorate;

        _getEvents = new SingleAction(InternalGetEventsAsync);

        _decoratedInstance.LogEntriesReceived += OnDecoratedInstanceLogEntriesReceived;
    }

    public event EventHandler<EventLogEntriesReceivedEventArgs<TId>>? LogEntriesReceived;

    public void Disable()
    {
        if (Interlocked.CompareExchange(ref _isEnabled, 0, 1) == 0)
        {
            // Already disabled
            return;
        }

        _decoratedInstance.Disable();
    }

    public void Enable()
    {
        if (Interlocked.CompareExchange(ref _isEnabled, 1, 0) != 0)
        {
            // Already enabled
            return;
        }

        _decoratedInstance.Enable();
    }

    public Task GetEventsAsync()
    {
        // Aggregates quick subsequent invocations into a single one. Assumes the GetEventsAsync
        // on the decorated instance does not complete synchronously.
        return _getEvents.RunAsync();
    }

    private Task InternalGetEventsAsync()
    {
        return _decoratedInstance.GetEventsAsync();
    }

    private void OnDecoratedInstanceLogEntriesReceived(object? sender, EventLogEntriesReceivedEventArgs<TId> eventArgs)
    {
        DispatchLogEntries(eventArgs);
        DispatchEventsProcessedAction(eventArgs);
    }

    private void DispatchLogEntries(EventLogEntriesReceivedEventArgs<TId> eventArgs)
    {
        var eventArgsWithoutEventsProcessedAction = new EventLogEntriesReceivedEventArgs<TId>(eventArgs.Entries)
        {
            VolumeId = eventArgs.VolumeId,
            Scope = eventArgs.Scope,
        };

        // Same events are dispatched to all subscribers, but without events processed action
        LogEntriesReceived?.Invoke(this, eventArgsWithoutEventsProcessedAction);
    }

    private void DispatchEventsProcessedAction(EventLogEntriesReceivedEventArgs<TId> eventArgs)
    {
        var eventsProcessedAggregator = new EventsProcessedActionAggregator(eventArgs.ConsiderEventsProcessed);
        var emptyEventArgsWithEventsProcessedAction = new EventLogEntriesReceivedEventArgs<TId>([], eventsProcessedAggregator.EventsProcessed)
        {
            VolumeId = eventArgs.VolumeId,
            Scope = eventArgs.Scope,
        };

        // Empty events list is dispatched to all subscribers, but with events processed action.
        // Events processed action is aggregated so that only the first invocation is passed through.
        LogEntriesReceived?.Invoke(this, emptyEventArgsWithEventsProcessedAction);
    }

    private class EventsProcessedActionAggregator
    {
        private readonly Action? _eventsProcessedAction;
        private int _hasProcessedEvents;

        public EventsProcessedActionAggregator(Action? eventsProcessedAction)
        {
            _eventsProcessedAction = eventsProcessedAction;
        }

        public void EventsProcessed()
        {
            if (Interlocked.Exchange(ref _hasProcessedEvents, 1) != 0)
            {
                // Events already processed
                return;
            }

            _eventsProcessedAction?.Invoke();
        }
    }
}
