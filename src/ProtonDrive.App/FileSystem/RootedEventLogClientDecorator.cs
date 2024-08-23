using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem;

internal sealed class RootedEventLogClientDecorator<TId> : IEventLogClient<TId>
{
    private readonly ILogger<RootedEventLogClientDecorator<TId>> _logger;
    private readonly IRootDirectory<TId> _rootDirectory;
    private readonly IRootableEventLogClient<TId> _decoratedInstance;

    public RootedEventLogClientDecorator(
        ILogger<RootedEventLogClientDecorator<TId>> logger,
        IRootDirectory<TId> rootDirectory,
        IRootableEventLogClient<TId> instanceToDecorate)
    {
        _logger = logger;
        _rootDirectory = rootDirectory;
        _decoratedInstance = instanceToDecorate;
    }

    public event EventHandler<EventLogEntriesReceivedEventArgs<TId>> LogEntriesReceived
    {
        add { _decoratedInstance.LogEntriesReceived += value; }
        remove { _decoratedInstance.LogEntriesReceived -= value; }
    }

    public void Enable()
    {
        _logger.LogDebug("Enabling directory change observation on \"{path}\"/-/{Id}", _rootDirectory.Path, _rootDirectory.Id);

        _decoratedInstance.Enable(_rootDirectory);
    }

    public void Disable()
    {
        _logger.LogDebug("Disabling directory change observation on \"{path}\"/-/{Id}", _rootDirectory.Path, _rootDirectory.Id);

        _decoratedInstance.Disable();
    }

    public Task GetEventsAsync() => Task.CompletedTask;
}
