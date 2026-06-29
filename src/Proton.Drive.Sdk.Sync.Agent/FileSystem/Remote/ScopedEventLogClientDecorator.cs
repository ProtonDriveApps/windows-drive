using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Remote;

/// <summary>
/// Replaces volume identity and event scope with the desired ones.
/// </summary>
internal sealed class ScopedEventLogClientDecorator : IEventLogClient<string>
{
    private readonly int _volumeId;
    private readonly string _scope;
    private readonly IEventLogClient<string> _decoratedInstance;

    public ScopedEventLogClientDecorator(
        int volumeId,
        string scope,
        IEventLogClient<string> instanceToDecorate)
    {
        _volumeId = volumeId;
        _scope = scope;
        _decoratedInstance = instanceToDecorate;

        _decoratedInstance.LogEntriesReceived += OnDecoratedInstanceLogEntriesReceived;
    }

    public event EventHandler<EventLogEntriesReceivedEventArgs<string>>? LogEntriesReceived;

    public void Enable() => _decoratedInstance.Enable();

    public void Disable() => _decoratedInstance.Disable();

    public Task GetEventsAsync() => _decoratedInstance.GetEventsAsync();

    private void OnDecoratedInstanceLogEntriesReceived(object? sender, EventLogEntriesReceivedEventArgs<string> eventArgs)
    {
        var rootedEventArgs = new EventLogEntriesReceivedEventArgs<string>(eventArgs.Entries, eventArgs.ConsiderEventsProcessed)
        {
            VolumeId = _volumeId,
            Scope = _scope,
        };

        LogEntriesReceived?.Invoke(this, rootedEventArgs);
    }
}
