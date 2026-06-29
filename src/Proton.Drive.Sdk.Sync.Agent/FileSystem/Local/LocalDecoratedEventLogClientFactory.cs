using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Adapter;
using Proton.Drive.Sdk.Sync.Agent.Mapping;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Local;

internal sealed class LocalDecoratedEventLogClientFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILocalEventLogClientFactory _undecoratedClientFactory;
    private readonly LocalRootMapForDeletionDetectionFactory _rootMapForDeletionDetectionFactory;
    private readonly IFileTransferAbortionStrategy<long> _fileTransferAbortionStrategy;
    private readonly IRootDeletionHandler _rootDeletionHandler;

    public LocalDecoratedEventLogClientFactory(
        ILoggerFactory loggerFactory,
        ILocalEventLogClientFactory undecoratedClientFactory,
        LocalRootMapForDeletionDetectionFactory rootMapForDeletionDetectionFactory,
        IFileTransferAbortionStrategy<long> fileTransferAbortionStrategy,
        IRootDeletionHandler rootDeletionHandler)
    {
        _loggerFactory = loggerFactory;
        _undecoratedClientFactory = undecoratedClientFactory;
        _rootMapForDeletionDetectionFactory = rootMapForDeletionDetectionFactory;
        _fileTransferAbortionStrategy = fileTransferAbortionStrategy;
        _rootDeletionHandler = rootDeletionHandler;
    }

    public IEventLogClient<long> GetClient(IReadOnlyCollection<RemoteToLocalMapping> mappings)
    {
        var clientMap = GetHostDeviceFolderClientMap(mappings);

        var cloudFilesMapping = mappings.SingleOrDefault(m => m is { HasSetupSucceeded: true, Type: MappingType.CloudFiles });
        if (cloudFilesMapping != null)
        {
            var undecoratedClient = CreateUndecoratedClient();

            clientMap = clientMap
                .Append(GetCloudFilesClientMap(cloudFilesMapping, undecoratedClient))
                .Concat(GetShareWithMeFileClientMap(mappings, cloudFilesMapping, undecoratedClient));
        }

        var eventScopeToClientMap = clientMap.ToDictionary(
            x => (x.EventScope, x.VolumeId),
            x => x.Client);

        var aggregatingClient = new AggregatingEventLogClient<long>(eventScopeToClientMap);

        var fileTransferAbortingClient = new FileTransferAbortingEventLogClientDecorator(aggregatingClient, _fileTransferAbortionStrategy);

        var (volumeId, nodeIdToRootMap) = _rootMapForDeletionDetectionFactory.Create(mappings);

        var rootDeletionDetectingClient = new RootDeletionDetectingEventLogClientDecorator<long>(
            new RootDeletionDetector<long>(_rootDeletionHandler, volumeId, nodeIdToRootMap),
            fileTransferAbortingClient);

        return rootDeletionDetectingClient;
    }

    private (string EventScope, int VolumeId, IEventLogClient<long> Client) GetCloudFilesClientMap(
        RemoteToLocalMapping mapping,
        IRootableEventLogClient<long> undecoratedClient)
    {
        // Based on cloud files mapping, a local event log client is created on account root folder.
        // It also covers foreign device root folders and shared with me items, if any.
        // Not successfully set up mappings are excluded.
        return (EventScope: mapping.Id.ToString(), VolumeId: mapping.Local.InternalVolumeId, Client: CreateClientForMapping(mapping, undecoratedClient));
    }

    private IEnumerable<(string EventScope, int VolumeId, IEventLogClient<long> Client)> GetHostDeviceFolderClientMap(
        IReadOnlyCollection<RemoteToLocalMapping> mappings)
    {
        // A local event log client is created per host device folder.
        // Not successfully set up mappings are excluded.
        return mappings
            .Where(m => m is { HasSetupSucceeded: true, Type: MappingType.HostDeviceFolder })
            .Select(m => (EventScope: m.Id.ToString(), VolumeId: m.Local.InternalVolumeId, Client: CreateClientForHostDeviceFolder(m)));
    }

    private IEnumerable<(string EventScope, int VolumeId, IEventLogClient<long> Client)> GetShareWithMeFileClientMap(
        IReadOnlyCollection<RemoteToLocalMapping> mappings,
        RemoteToLocalMapping cloudFilesMapping,
        IRootableEventLogClient<long> undecoratedClient)
    {
        // Shared with me files are placed into virtual roots, each having a unique internal volume ID, but sharing event scope of cloud files client
        var eventScope = cloudFilesMapping.Id.ToString();

        var sharedWithMeRootFolderMapping = mappings.SingleOrDefault(m => m is { HasSetupSucceeded: true, Type: MappingType.SharedWithMeRootFolder });
        if (sharedWithMeRootFolderMapping == null)
        {
            return [];
        }

        var parentFolderId = sharedWithMeRootFolderMapping.Local.RootFolderId;

        return mappings
            .Where(m => m is { HasSetupSucceeded: true, Type: MappingType.SharedWithMeItem, Remote.RootItemType: LinkType.File })
            .Select(m => (EventScope: eventScope, VolumeId: m.Local.InternalVolumeId, Client: CreateClientForSharedWithMeFile(m, parentFolderId, eventScope, undecoratedClient)));
    }

    private static IRootDirectory<long> CreateRootDirectory(RemoteToLocalMapping mapping)
    {
        switch (mapping.Type)
        {
            case MappingType.CloudFiles:
                if (!mapping.TryGetAccountRootFolderPath(out var accountRootFolderPath))
                {
                    throw new InvalidOperationException($"Unable to obtain account root folder path from mapping Id={mapping.Id}");
                }

                // We do not know ID of this folder. Currently, folder ID is not used by the event log client.
                return new LocalRootDirectory(accountRootFolderPath, id: 0);

            case MappingType.HostDeviceFolder:
                return new LocalRootDirectory(mapping.Local);

            default:
                throw new InvalidEnumArgumentException(nameof(mapping.Type), (int)mapping.Type, typeof(MappingType));
        }
    }

    private IEventLogClient<long> CreateClientForHostDeviceFolder(RemoteToLocalMapping mapping)
    {
        var undecoratedClient = CreateUndecoratedClient();

        return CreateClientForMapping(mapping, undecoratedClient);
    }

    private IEventLogClient<long> CreateClientForMapping(RemoteToLocalMapping mapping, IRootableEventLogClient<long> undecoratedClient)
    {
        var rootedClient = new RootedEventLogClientDecorator(
            _loggerFactory.CreateLogger<RootedEventLogClientDecorator>(),
            CreateRootDirectory(mapping),
            undecoratedClient);

        var logger = _loggerFactory.CreateLogger<LoggingEventLogClientDecorator<long>>();

        return new LoggingEventLogClientDecorator<long>(logger, mapping.Local.InternalVolumeId, scope: mapping.Id.ToString(), rootedClient);
    }

    private IEventLogClient<long> CreateClientForSharedWithMeFile(RemoteToLocalMapping mapping, long parentFolderId, string eventScope, IRootableEventLogClient<long> undecoratedClient)
    {
        var fileName = Path.GetFileName(mapping.Local.Path);

        var virtualRootClient = new VirtualFileRootEventLogClientDecorator(parentFolderId, fileName, undecoratedClient);

        var logger = _loggerFactory.CreateLogger<LoggingEventLogClientDecorator<long>>();

        return new LoggingEventLogClientDecorator<long>(logger, mapping.Local.InternalVolumeId, eventScope, virtualRootClient);
    }

    private IRootableEventLogClient<long> CreateUndecoratedClient()
    {
        return _undecoratedClientFactory.Create();
    }
}
