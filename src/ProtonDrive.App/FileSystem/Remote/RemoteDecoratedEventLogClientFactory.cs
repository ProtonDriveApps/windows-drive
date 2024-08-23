using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Settings;
using ProtonDrive.Client;
using ProtonDrive.Client.Configuration;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Property;

namespace ProtonDrive.App.FileSystem.Remote;

internal sealed class RemoteDecoratedEventLogClientFactory
{
    public const string ShareEventAnchorIdPrefix = "LatestEventId-";
    public const string VolumeEventAnchorIdPrefix = "VolumeEventAnchorId-";

    private readonly DriveApiConfig _driveConfig;
    private readonly IRemoteEventLogClientFactory _undecoratedClientFactory;
    private readonly RemoteRootMapForDeletionDetectionFactory _rootMapForDeletionDetectionFactory;
    private readonly ISwitchingToVolumeEventsHandler _switchingToVolumeEventsHandler;
    private readonly IRootDeletionHandler _rootDeletionHandler;
    private readonly ILoggerFactory _loggerFactory;

    public RemoteDecoratedEventLogClientFactory(
        DriveApiConfig driveConfig,
        IRemoteEventLogClientFactory undecoratedClientFactory,
        RemoteRootMapForDeletionDetectionFactory rootMapForDeletionDetectionFactory,
        ISwitchingToVolumeEventsHandler switchingToVolumeEventsHandler,
        IRootDeletionHandler rootDeletionHandler,
        ILoggerFactory loggerFactory)
    {
        _driveConfig = driveConfig;
        _undecoratedClientFactory = undecoratedClientFactory;
        _rootMapForDeletionDetectionFactory = rootMapForDeletionDetectionFactory;
        _switchingToVolumeEventsHandler = switchingToVolumeEventsHandler;
        _rootDeletionHandler = rootDeletionHandler;
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

        var eventScopeToClientMap = (isVolumeBased
            ? GetEventScopeToClientMapForOwnVolumeEvents(mappings, propertyRepository)
            : GetEventScopeToClientMapForOwnShareEvents(mappings, propertyRepository))
            .Concat(GetEventScopeToClientMapForForeignVolumeEvents(mappings, propertyRepository));

        var (volumeId, nodeIdToRootMap) = _rootMapForDeletionDetectionFactory.Create(mappings);

        var eventScopeToClientMapDictionary = eventScopeToClientMap.ToDictionary(
            x => x.EventScope,
            x => (x.VolumeId, x.Client));

        return
            new RootDeletionDetectingEventLogClientDecorator<string>(
                new RootDeletionDetector<string>(_rootDeletionHandler, volumeId, nodeIdToRootMap),
                new AggregatingEventLogClient<string>(eventScopeToClientMapDictionary, SharedWithMeFileRootDirectoryMaps<string>.Empty));
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
                EventScope: x.RemoteVolumeId,
                VolumeId: x.InternalVolumeId,
                Client: CreateClientForVolume(VolumeType.Own, x.InternalVolumeId, x.RemoteVolumeId, propertyRepository)));
    }

    private IEnumerable<(string EventScope, int VolumeId, IEventLogClient<string> Client)> GetEventScopeToClientMapForForeignVolumeEvents(
        IEnumerable<RemoteToLocalMapping> mappings,
        IPropertyRepository propertyRepository)
    {
        // A remote event log client is created per remote volume.
        // Each remote volume belongs to a different event scope.
        // Unsuccessfully set up mappings are excluded.
        return mappings
            .Where(m => m is { HasSetupSucceeded: true, Type: MappingType.SharedWithMeItem })
            .Select(m => (m.Remote.InternalVolumeId, RemoteVolumeId: m.Remote.VolumeId ?? string.Empty))
            .Distinct()
            .Where(x => x.RemoteVolumeId != string.Empty)
            .Select(x => (
                EventScope: x.RemoteVolumeId,
                VolumeId: x.InternalVolumeId,
                Client: CreateClientForVolume(VolumeType.Foreign, x.InternalVolumeId, x.RemoteVolumeId, propertyRepository)));
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

    private IEventLogClient<string> CreateClientForVolume(
        VolumeType volumeType,
        int internalVolumeId,
        string remoteVolumeId,
        IPropertyRepository propertyRepository)
    {
        var anchorIdRepository = new CachingRepository<string>(new NamedProperty<string>(VolumeEventAnchorIdPrefix + remoteVolumeId, propertyRepository));

        return new LoggingEventLogClientDecorator<string>(
            _loggerFactory.CreateLogger<LoggingEventLogClientDecorator<string>>(),
            CreateUndecoratedClientForVolume(volumeType, remoteVolumeId, anchorIdRepository),
            internalVolumeId,
            remoteVolumeId);
    }

    private IEventLogClient<string> CreateClientForShare(int internalVolumeId, string shareId, IPropertyRepository propertyRepository)
    {
        var anchorIdRepository = new CachingRepository<string>(new NamedProperty<string>(ShareEventAnchorIdPrefix + shareId, propertyRepository));

        return new LoggingEventLogClientDecorator<string>(
            _loggerFactory.CreateLogger<LoggingEventLogClientDecorator<string>>(),
            CreateUndecoratedClientForShare(shareId, anchorIdRepository),
            internalVolumeId,
            shareId);
    }

    private IEventLogClient<string> CreateUndecoratedClientForVolume(VolumeType volumeType, string volumeId, IRepository<string> anchorIdRepository)
    {
        var eventsPollingInterval = volumeType switch
        {
            VolumeType.Own => _driveConfig.EventsPollingInterval.RandomizedWithDeviation(0.2),
            VolumeType.Foreign => _driveConfig.ForeignVolumeEventsPollingInterval.RandomizedWithDeviation(0.2),
            _ => throw new InvalidEnumArgumentException(nameof(volumeType), (int)volumeType, typeof(VolumeType)),
        };

        return _undecoratedClientFactory.CreateClientForVolume(volumeId, anchorIdRepository, eventsPollingInterval);
    }

    private IEventLogClient<string> CreateUndecoratedClientForShare(string shareId, IRepository<string> anchorIdRepository)
    {
        return _undecoratedClientFactory.CreateClientForShare(shareId, anchorIdRepository, _driveConfig.EventsPollingInterval.RandomizedWithDeviation(0.2));
    }
}
