using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Client.Configuration;
using Proton.Drive.Sdk.Sync.Client.RemoteNodes;
using Proton.Drive.Sdk.Sync.Client.Shares.Events;
using Proton.Drive.Sdk.Sync.Client.Volumes.Events;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Repository;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Client;

internal class RemoteEventLogClientFactory : IRemoteEventLogClientFactory
{
    private readonly DriveApiConfig _apiConfig;
    private readonly IVolumeEventClient _volumeEventClient;
    private readonly IShareEventClient _shareEventClient;
    private readonly IRemoteNodeService _remoteNodeService;
    private readonly IScheduler _scheduler;
    private readonly IClock _clock;
    private readonly ILogger<RemoteEventLogClient> _logger;

    public RemoteEventLogClientFactory(
        DriveApiConfig apiConfig,
        IVolumeEventClient volumeEventClient,
        IShareEventClient shareEventClient,
        IRemoteNodeService remoteNodeService,
        IScheduler scheduler,
        IClock clock,
        ILogger<RemoteEventLogClient> logger)
    {
        _apiConfig = apiConfig;
        _volumeEventClient = volumeEventClient;
        _shareEventClient = shareEventClient;
        _remoteNodeService = remoteNodeService;
        _scheduler = scheduler;
        _clock = clock;
        _logger = logger;
    }

    public IEventLogClient<string> CreateClientForShare(string shareId, IRepository<string> anchorIdRepository, TimeSpan pollInterval)
    {
        return new RemoteEventLogClient(
            isVolumeBased: false,
            shareId,
            anchorIdRepository,
            pollInterval,
            _apiConfig.EventsThrottleInterval,
            _volumeEventClient,
            _shareEventClient,
            _remoteNodeService,
            _scheduler,
            _clock,
            _logger);
    }

    public IEventLogClient<string> CreateClientForVolume(string volumeId, IRepository<string> anchorIdRepository, TimeSpan pollInterval)
    {
        return new RemoteEventLogClient(
            isVolumeBased: true,
            volumeId,
            anchorIdRepository,
            pollInterval,
            _apiConfig.EventsThrottleInterval,
            _volumeEventClient,
            _shareEventClient,
            _remoteNodeService,
            _scheduler,
            _clock,
            _logger);
    }
}
