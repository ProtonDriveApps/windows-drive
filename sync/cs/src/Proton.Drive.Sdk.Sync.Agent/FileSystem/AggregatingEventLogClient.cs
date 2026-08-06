using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem;

internal sealed class AggregatingEventLogClient<TId> : IEventLogClient<TId>
{
    private readonly IReadOnlyDictionary<(string EventScope, int VolumeId), IEventLogClient<TId>> _eventScopeToClientMap;
    private IReadOnlyCollection<EventSubscription>? _eventSubscriptions;

    public AggregatingEventLogClient(IReadOnlyDictionary<(string EventScope, int VolumeId), IEventLogClient<TId>> eventScopeToClientMap)
    {
        _eventScopeToClientMap = eventScopeToClientMap;
    }

    public event EventHandler<EventLogEntriesReceivedEventArgs<TId>>? LogEntriesReceived
    {
        add
        {
            var isFirstSubscription = LogEntriesReceivedHandlers is null;

            LogEntriesReceivedHandlers += value;

            if (isFirstSubscription && LogEntriesReceivedHandlers is not null)
            {
                SubscribeToDecoratedClients();
            }
        }
        remove
        {
            var couldBeLastSubscription = LogEntriesReceivedHandlers is not null;

            LogEntriesReceivedHandlers -= value;

            if (couldBeLastSubscription && LogEntriesReceivedHandlers is null)
            {
                UnsubscribeFromDecoratedClients();
            }
        }
    }

    private EventHandler<EventLogEntriesReceivedEventArgs<TId>>? LogEntriesReceivedHandlers { get; set; }

    public void Enable()
    {
        foreach (var client in _eventScopeToClientMap.Values)
        {
            client.Enable();
        }
    }

    public void Disable()
    {
        foreach (var client in _eventScopeToClientMap.Values)
        {
            client.Disable();
        }
    }

    public async Task GetEventsAsync()
    {
        await Task.WhenAll(_eventScopeToClientMap.Values.Select(x => x.GetEventsAsync())).ConfigureAwait(false);
    }

    private void SubscribeToDecoratedClients()
    {
        _eventSubscriptions = _eventScopeToClientMap
            .Select(pair => new EventSubscription(this, pair.Value, volumeId: pair.Key.VolumeId, eventScope: pair.Key.EventScope))
            .ToList();
    }

    private void UnsubscribeFromDecoratedClients()
    {
        if (_eventSubscriptions is null)
        {
            return;
        }

        var eventSubscriptions = _eventSubscriptions;
        _eventSubscriptions = default;

        foreach (var subscription in eventSubscriptions)
        {
            subscription.Dispose();
        }
    }

    private sealed class EventSubscription : IDisposable
    {
        private readonly AggregatingEventLogClient<TId> _owner;
        private readonly IEventLogClient<TId> _client;
        private readonly int _volumeId;
        private readonly string _eventScope;

        public EventSubscription(AggregatingEventLogClient<TId> owner, IEventLogClient<TId> client, int volumeId, string eventScope)
        {
            _owner = owner;
            _client = client;
            _volumeId = volumeId;
            _eventScope = eventScope;

            _client.LogEntriesReceived += Handle;
        }

        public void Dispose()
        {
            _client.LogEntriesReceived -= Handle;
        }

        private void Handle(object? sender, EventLogEntriesReceivedEventArgs<TId> eventArgs)
        {
            eventArgs.VolumeId = _volumeId;
            eventArgs.Scope = _eventScope;
            _owner.LogEntriesReceivedHandlers?.Invoke(_client, eventArgs);
        }
    }
}
