using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Devices;
using Proton.Drive.Sdk.Sync.Agent.Mapping;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Client.Configuration;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Property;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Repository;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Remote;

internal sealed class RemoteDecoratedEventLogClientFactory
{
    public const string ShareEventAnchorIdPrefix = "LatestEventId-";
    public const string VolumeEventAnchorIdPrefix = "VolumeEventAnchorId-";

    private readonly DriveApiConfig _driveConfig;
    private readonly IRemoteEventLogClientFactory _undecoratedClientFactory;
    private readonly RemoteRootMapForDeletionDetectionFactory _rootMapForDeletionDetectionFactory;
    private readonly ISwitchingToVolumeEventsHandler _switchingToVolumeEventsHandler;
    private readonly IRootDeletionHandler _rootDeletionHandler;
    private readonly IRemoteDeviceEventsAware _remoteDeviceEventsAware;
    private readonly ILoggerFactory _loggerFactory;

    public RemoteDecoratedEventLogClientFactory(
        DriveApiConfig driveConfig,
        IRemoteEventLogClientFactory undecoratedClientFactory,
        RemoteRootMapForDeletionDetectionFactory rootMapForDeletionDetectionFactory,
        ISwitchingToVolumeEventsHandler switchingToVolumeEventsHandler,
        IRootDeletionHandler rootDeletionHandler,
        IRemoteDeviceEventsAware remoteDeviceEventsAware,
        ILoggerFactory loggerFactory)
    {
        _driveConfig = driveConfig;
        _undecoratedClientFactory = undecoratedClientFactory;
        _rootMapForDeletionDetectionFactory = rootMapForDeletionDetectionFactory;
        _switchingToVolumeEventsHandler = switchingToVolumeEventsHandler;
        _rootDeletionHandler = rootDeletionHandler;
        _remoteDeviceEventsAware = remoteDeviceEventsAware;
        _loggerFactory = loggerFactory;
    }

    private enum VolumeType
    {
        Own,
        Foreign,
    }

    public async Task<IEventLogClient<string>> GetClientAsync(
        IReadOnlyCollection<RemoteToLocalMapping> mappings,
        IPropertyRepository propertyRepository,
        CancellationToken cancellationToken)
    {
        var isVolumeBased = await _switchingToVolumeEventsHandler.TrySwitchAsync(mappings, cancellationToken).ConfigureAwait(false);

        var clientMap = (isVolumeBased
            ? GetEventScopeToClientMapForOwnVolumeEvents(mappings, propertyRepository)
            : GetEventScopeToClientMapForOwnShareEvents(mappings, propertyRepository))
            .Concat(GetEventScopeToClientMapForForeignVolumeEvents(mappings, propertyRepository));

        var (volumeId, nodeIdToRootMap) = _rootMapForDeletionDetectionFactory.Create(mappings);

        Dictionary<(string EventScope, int VolumeId), IEventLogClient<string>> eventScopeToClientMap = clientMap.ToDictionary(
            x => (x.EventScope, x.VolumeId),
            x => x.Client);

        return
            new RootDeletionDetectingEventLogClientDecorator<string>(
                new RootDeletionDetector<string>(_rootDeletionHandler, volumeId, nodeIdToRootMap),
                new AggregatingEventLogClient<string>(eventScopeToClientMap));
    }

    private IEnumerable<(string EventScope, int VolumeId, IEventLogClient<string> Client)> GetEventScopeToClientMapForOwnVolumeEvents(
        IEnumerable<RemoteToLocalMapping> mappings,
        IPropertyRepository propertyRepository)
    {
        // A remote event log client is created per remote volume.
        // Each remote volume belongs to a different event scope.
        // Unsuccessfully set up mappings are excluded.
        return mappings
            .Where(m => m is { HasSetupSucceeded: true, Type: MappingType.CloudFiles or MappingType.HostDeviceFolder or MappingType.ForeignDevice })
            .Select(m => (m.Remote.InternalVolumeId, RemoteVolumeId: m.Remote.VolumeId ?? string.Empty))
            .Distinct()
            .Where(x => x.RemoteVolumeId != string.Empty)
            .Select(x => (
                EventScope: RootPropertyProvider.GetEventScope(x.InternalVolumeId),
                VolumeId: x.InternalVolumeId,
                Client: CreateClientForOwnVolume(x.InternalVolumeId, x.RemoteVolumeId, propertyRepository)));
    }

    private IEnumerable<(string EventScope, int VolumeId, IEventLogClient<string> Client)> GetEventScopeToClientMapForForeignVolumeEvents(
        IReadOnlyCollection<RemoteToLocalMapping> mappings,
        IPropertyRepository propertyRepository)
    {
        // A remote event log client is created per foreign remote volume.
        // Foreign volumes with no successfully set up mappings are excluded.
        var remoteVolumeIds = mappings
            .Where(m => m is
            {
                HasSetupSucceeded: true,
                Type: MappingType.SharedWithMeItem,
            })
            .Select(m => m.Remote.VolumeId ?? string.Empty)
            .Distinct()
            .Where(x => !string.IsNullOrEmpty(x));

        return remoteVolumeIds
            .SelectMany(remoteVolumeId => GetEventScopeToClientMapForSharedWithMeItems(remoteVolumeId, mappings, propertyRepository));
    }

    private IEnumerable<(string EventScope, int VolumeId, IEventLogClient<string> Client)> GetEventScopeToClientMapForSharedWithMeItems(
        string remoteVolumeId,
        IReadOnlyCollection<RemoteToLocalMapping> mappings,
        IPropertyRepository propertyRepository)
    {
        var undecoratedClient = CreateUndecoratedClientForVolume(VolumeType.Foreign, remoteVolumeId, propertyRepository);

        var dispatchingClient = new DispatchingEventLogClientDecorator<string>(undecoratedClient);

        // Unsuccessfully set up mappings are included, because we must process events for all roots, even disabled ones.
        // If we skip events, we will lose consistency between local cache and remote file system state.
        var mapForFolders = mappings
            .Where(m => m is { Type: MappingType.SharedWithMeItem } && m.Remote.VolumeId == remoteVolumeId && m.Remote.RootItemType is LinkType.Folder)
            .Select(m => (
                EventScope: RootPropertyProvider.GetEventScope(m.Remote.InternalVolumeId),
                VolumeId: m.Remote.InternalVolumeId,
                Client: CreateClientForSharedWithMeFolder(m, dispatchingClient)));

        // Unsuccessfully set up mappings are included, because we must process events for all roots, even disabled ones.
        // If we skip events, we will lose consistency between local cache and remote file system state.
        var mapForFiles = mappings
            .Where(m => m is { Type: MappingType.SharedWithMeItem } && m.Remote.VolumeId == remoteVolumeId && m.Remote.RootItemType is LinkType.File)
            .Select(m => (
                EventScope: RootPropertyProvider.GetEventScope(m.Remote.InternalVolumeId),
                VolumeId: m.Remote.InternalVolumeId,
                Client: CreateClientForSharedWithMeFile(m, dispatchingClient)));

        return mapForFolders.Union(mapForFiles);
    }

    private IEnumerable<(string EventScope, int VolumeId, IEventLogClient<string> Client)> GetEventScopeToClientMapForOwnShareEvents(
        IEnumerable<RemoteToLocalMapping> mappings,
        IPropertyRepository propertyRepository)
    {
        // A remote event log client is created per remote share.
        // Each remote share belongs to a different event scope.
        // Unsuccessfully set up mappings are excluded.
        return mappings
            .Where(m => m is { HasSetupSucceeded: true, Type: MappingType.CloudFiles or MappingType.HostDeviceFolder or MappingType.ForeignDevice })
            .Select(m => (m.Remote.InternalVolumeId, ShareId: m.Remote.ShareId ?? string.Empty))
            .Distinct()
            .Where(x => x.ShareId != string.Empty)
            .Select(x => (
                EventScope: x.ShareId,
                VolumeId: x.InternalVolumeId,
                Client: CreateClientForShare(x.InternalVolumeId, x.ShareId, propertyRepository)));
    }

    private IEventLogClient<string> CreateClientForOwnVolume(
        int internalVolumeId,
        string remoteVolumeId,
        IPropertyRepository propertyRepository)
    {
        var undecoratedClient = CreateUndecoratedClientForVolume(VolumeType.Own, remoteVolumeId, propertyRepository);

        var scopedClient = new ScopedEventLogClientDecorator(
            internalVolumeId,
            scope: RootPropertyProvider.GetEventScope(internalVolumeId),
            undecoratedClient);

        var remoteDeviceClientDecorator = new RemoteDeviceEventLogClientDecorator(
            _remoteDeviceEventsAware,
            scopedClient);

        return new LoggingEventLogClientDecorator<string>(
            _loggerFactory.CreateLogger<LoggingEventLogClientDecorator<string>>(),
            internalVolumeId,
            remoteVolumeId,
            remoteDeviceClientDecorator);
    }

    private IEventLogClient<string> CreateClientForSharedWithMeFolder(RemoteToLocalMapping mapping, IEventLogClient<string> dispatchingClient)
    {
        var scopedClient = CreateScopedDecorator(mapping, dispatchingClient);

        return CreateLoggingDecorator(mapping, scopedClient);
    }

    private IEventLogClient<string> CreateClientForSharedWithMeFile(RemoteToLocalMapping mapping, IEventLogClient<string> dispatchingClient)
    {
        var scopedClient = CreateScopedDecorator(mapping, dispatchingClient);

        var virtualRootClient = CreateVirtualFileRootDecorator(mapping, scopedClient);

        return CreateLoggingDecorator(mapping, virtualRootClient);
    }

    private ScopedEventLogClientDecorator CreateScopedDecorator(RemoteToLocalMapping mapping, IEventLogClient<string> client)
    {
        var internalVolumeId = mapping.Remote.InternalVolumeId;

        return new ScopedEventLogClientDecorator(
            internalVolumeId,
            scope: RootPropertyProvider.GetEventScope(internalVolumeId),
            client);
    }

    private VirtualFileRootEventLogClientDecorator CreateVirtualFileRootDecorator(RemoteToLocalMapping mapping, IEventLogClient<string> client)
    {
        var fileId = mapping.Remote.RootLinkId;
        var fileName = Path.GetFileName(mapping.Local.Path);
        var virtualFolderId = RootPropertyProvider.GetVirtualRootFolderId(mapping.Id);

        Ensure.NotNullOrEmpty(fileId, nameof(fileId));

        return new VirtualFileRootEventLogClientDecorator(
            fileId,
            fileName,
            virtualFolderId,
            client);
    }

    private LoggingEventLogClientDecorator<string> CreateLoggingDecorator(RemoteToLocalMapping mapping, IEventLogClient<string> client)
    {
        var internalVolumeId = mapping.Remote.InternalVolumeId;

        return new LoggingEventLogClientDecorator<string>(
            _loggerFactory.CreateLogger<LoggingEventLogClientDecorator<string>>(),
            volumeId: internalVolumeId,
            scope: RootPropertyProvider.GetEventScope(internalVolumeId),
            client);
    }

    private IEventLogClient<string> CreateClientForShare(int internalVolumeId, string shareId, IPropertyRepository propertyRepository)
    {
        var undecoratedClient = CreateUndecoratedClientForShare(shareId, propertyRepository);

        return new LoggingEventLogClientDecorator<string>(
            _loggerFactory.CreateLogger<LoggingEventLogClientDecorator<string>>(),
            internalVolumeId,
            shareId,
            undecoratedClient);
    }

    private IEventLogClient<string> CreateUndecoratedClientForVolume(VolumeType volumeType, string remoteVolumeId, IPropertyRepository propertyRepository)
    {
        var anchorIdRepository = new CachingRepository<string>(new NamedProperty<string>(VolumeEventAnchorIdPrefix + remoteVolumeId, propertyRepository));

        var eventsPollingInterval = volumeType switch
        {
            VolumeType.Own => _driveConfig.EventsPollingInterval.RandomizedWithDeviation(0.2),
            VolumeType.Foreign => _driveConfig.ForeignVolumeEventsPollingInterval.RandomizedWithDeviation(0.2),
            _ => throw new InvalidEnumArgumentException(nameof(volumeType), (int)volumeType, typeof(VolumeType)),
        };

        return _undecoratedClientFactory.CreateClientForVolume(remoteVolumeId, anchorIdRepository, eventsPollingInterval);
    }

    private IEventLogClient<string> CreateUndecoratedClientForShare(string shareId, IPropertyRepository propertyRepository)
    {
        var anchorIdRepository = new CachingRepository<string>(new NamedProperty<string>(ShareEventAnchorIdPrefix + shareId, propertyRepository));

        var eventsPollingInterval = _driveConfig.EventsPollingInterval.RandomizedWithDeviation(0.2);

        return _undecoratedClientFactory.CreateClientForShare(shareId, anchorIdRepository, eventsPollingInterval);
    }
}
