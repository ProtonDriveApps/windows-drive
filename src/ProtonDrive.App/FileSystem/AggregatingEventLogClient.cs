using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MoreLinq;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem;

internal sealed class AggregatingEventLogClient<TId> : IEventLogClient<TId>
{
    private readonly IReadOnlyDictionary<string, (int VolumeId, IEventLogClient<TId> Client)> _eventScopeToClientMap;
    private readonly ISharedWithMeFileRootDirectoryMaps _sharedWithMeFileRootDirectoryMaps;

    private IReadOnlyCollection<EventSubscription>? _eventSubscriptions;

    public AggregatingEventLogClient(
        IReadOnlyDictionary<string, (int VolumeId, IEventLogClient<TId> Client)> eventScopeToClientMap,
        ISharedWithMeFileRootDirectoryMaps sharedWithMeFileRootDirectoryMaps)
    {
        _eventScopeToClientMap = eventScopeToClientMap;
        _sharedWithMeFileRootDirectoryMaps = sharedWithMeFileRootDirectoryMaps;
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
        foreach (var (_, client) in _eventScopeToClientMap.Values)
        {
            client.Enable();
        }
    }

    public void Disable()
    {
        foreach (var (_, client) in _eventScopeToClientMap.Values)
        {
            client.Disable();
        }
    }

    public async Task GetEventsAsync()
    {
        await Task.WhenAll(_eventScopeToClientMap.Values.Select(x => x.Client.GetEventsAsync())).ConfigureAwait(false);
    }

    private void SubscribeToDecoratedClients()
    {
        _eventSubscriptions = _eventScopeToClientMap.Select(pair => new EventSubscription(this, pair.Value.Client, pair.Value.VolumeId, pair.Key)).ToList();
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

        private void Handle(object? sender, EventLogEntriesReceivedEventArgs<TId> e)
        {
            var groupEventArgs = e.Entries
                .Select(x => (
                    VolumeId: GetVolumeIdFromEntryName(x.Name),
                    Entry: x))
                .GroupAdjacent(x => x.VolumeId)
                .Select(
                    x => new EventLogEntriesReceivedEventArgs<TId>(x.Select(y => y.Entry).ToList())
                    {
                        VolumeId = x.Key,
                        Scope = _eventScope,
                    });

            foreach (var eventArgs in groupEventArgs)
            {
                _owner.LogEntriesReceivedHandlers?.Invoke(_client, eventArgs);
            }
        }

        private int GetVolumeIdFromEntryName(string? entryName)
        {
            if (entryName is not null && _owner._sharedWithMeFileRootDirectoryMaps.TryGetMappingIdFromSharedWithMeFileName(entryName, out var mappingId))
            {
                return VirtualInternalVolumeIdProvider.GetId(_volumeId, mappingId.Value);
            }

            return _volumeId;
        }
    }
}
