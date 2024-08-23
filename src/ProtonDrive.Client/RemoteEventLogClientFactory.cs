using System;
using Microsoft.Extensions.Logging;
using ProtonDrive.Client.RemoteNodes;
using ProtonDrive.Client.Shares.Events;
using ProtonDrive.Client.Volumes.Events;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client;

internal class RemoteEventLogClientFactory : IRemoteEventLogClientFactory
{
    private readonly IVolumeEventClient _volumeEventClient;
    private readonly IShareEventClient _shareEventClient;
    private readonly IRemoteNodeService _remoteNodeService;
    private readonly IScheduler _scheduler;
    private readonly ILogger<RemoteEventLogClient> _logger;

    public RemoteEventLogClientFactory(
        IVolumeEventClient volumeEventClient,
        IShareEventClient shareEventClient,
        IRemoteNodeService remoteNodeService,
        IScheduler scheduler,
        ILogger<RemoteEventLogClient> logger)
    {
        _volumeEventClient = volumeEventClient;
        _shareEventClient = shareEventClient;
        _remoteNodeService = remoteNodeService;
        _scheduler = scheduler;
        _logger = logger;
    }

    public IEventLogClient<string> CreateClientForShare(string shareId, IRepository<string> anchorIdRepository, TimeSpan pollInterval)
    {
        return new RemoteEventLogClient(
            isVolumeBased: false,
            shareId,
            anchorIdRepository,
            pollInterval,
            _volumeEventClient,
            _shareEventClient,
            _remoteNodeService,
            _scheduler,
            _logger);
    }

    public IEventLogClient<string> CreateClientForVolume(string volumeId, IRepository<string> anchorIdRepository, TimeSpan pollInterval)
    {
        return new RemoteEventLogClient(
            isVolumeBased: true,
            volumeId,
            anchorIdRepository,
            pollInterval,
            _volumeEventClient,
            _shareEventClient,
            _remoteNodeService,
            _scheduler,
            _logger);
    }
}
