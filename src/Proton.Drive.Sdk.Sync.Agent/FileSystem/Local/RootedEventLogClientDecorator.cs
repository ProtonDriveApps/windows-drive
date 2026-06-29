using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Local;

internal sealed class RootedEventLogClientDecorator : IEventLogClient<long>
{
    private readonly ILogger<RootedEventLogClientDecorator> _logger;
    private readonly IRootDirectory<long> _rootDirectory;
    private readonly IRootableEventLogClient<long> _decoratedInstance;

    public RootedEventLogClientDecorator(
        ILogger<RootedEventLogClientDecorator> logger,
        IRootDirectory<long> rootDirectory,
        IRootableEventLogClient<long> instanceToDecorate)
    {
        _logger = logger;
        _rootDirectory = rootDirectory;
        _decoratedInstance = instanceToDecorate;
    }

    public event EventHandler<EventLogEntriesReceivedEventArgs<long>> LogEntriesReceived
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
