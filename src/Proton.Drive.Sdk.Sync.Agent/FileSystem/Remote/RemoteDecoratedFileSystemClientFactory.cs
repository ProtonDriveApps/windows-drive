using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Account;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Remote;

internal sealed class RemoteDecoratedFileSystemClientFactory
{
    private readonly IRemoteFileSystemClientFactory _undecoratedClientFactory;
    private readonly IUserService _userService;
    private readonly ISwitchingToVolumeEventsHandler _switchingToVolumeEventsHandler;
    private readonly ILoggerFactory _loggerFactory;

    public RemoteDecoratedFileSystemClientFactory(
        IRemoteFileSystemClientFactory undecoratedClientFactory,
        IUserService userService,
        ISwitchingToVolumeEventsHandler switchingToVolumeEventsHandler,
        ILoggerFactory loggerFactory)
    {
        _undecoratedClientFactory = undecoratedClientFactory;
        _userService = userService;
        _switchingToVolumeEventsHandler = switchingToVolumeEventsHandler;
        _loggerFactory = loggerFactory;
    }

    public IFileSystemClient<string> GetClient(IReadOnlyCollection<RemoteToLocalMapping> mappings)
    {
        // A dummy (offline) client is created for non-successfully set up mappings.
        // If all mappings on the same remote share are non-successfully set up,
        // the remote file system client for share is still created, but gets abandoned.
        var rootToClientMap =
            GetOwnVolumeRootToClientMap(mappings)
                .Concat(GetForeignVolumeRootToClientMap(mappings))
                .ToDictionary(x => x.Root, x => x.Client);

        return new LoggingFileSystemClientDecorator<string>(
            _loggerFactory.CreateLogger<LoggingFileSystemClientDecorator<string>>(),
            new DispatchingFileSystemClient<string>(rootToClientMap));
    }

    private static IFileSystemClient<string> CreateClientForMapping(RemoteToLocalMapping mapping, IFileSystemClient<string> clientForShare)
    {
        return mapping.HasSetupSucceeded
            ? new RootedFileSystemClientDecorator(new RemoteRootDirectory(mapping), clientForShare)
            : new OfflineFileSystemClient<string>();
    }

    private IEnumerable<(RootInfo<string> Root, IFileSystemClient<string> Client)> GetOwnVolumeRootToClientMap(
        IEnumerable<RemoteToLocalMapping> mappings)
    {
        return from mapping in mappings
               where mapping.Type is MappingType.CloudFiles or MappingType.ForeignDevice or MappingType.HostDeviceFolder
               group mapping by (mapping.Remote.VolumeId, mapping.Remote.ShareId) into shareMappings
               let shareClient = CreateOwnVolumeClientForShare(shareMappings.Key.VolumeId, shareMappings.Key.ShareId)
               from shareMapping in shareMappings
               select (
                   Root: CreateRoot(shareMapping),
                   Client: CreateClientForMapping(shareMapping, shareClient));
    }

    private IEnumerable<(RootInfo<string> Root, IFileSystemClient<string> Client)> GetForeignVolumeRootToClientMap(
        IReadOnlyCollection<RemoteToLocalMapping> mappings)
    {
        var sharedWithMeRootFolderMapping = mappings.FirstOrDefault(m => m.Type is MappingType.SharedWithMeRootFolder);

        if (sharedWithMeRootFolderMapping is null)
        {
            return mappings
                .Where(x => x.Type is MappingType.SharedWithMeItem)
                .Select(mapping => (
                        Root: CreateRoot(mapping),
                        Client: (IFileSystemClient<string>)new OfflineFileSystemClient<string>()));
        }

        return from mapping in mappings
               where mapping.Type is MappingType.SharedWithMeItem
               let client = CreateClientForSharedWithMeItem(mapping)
               select (
                   Root: CreateRoot(mapping),
                   Client: CreateClientForMapping(mapping, client));
    }

    private RootInfo<string> CreateRoot(RemoteToLocalMapping mapping)
    {
        var isUsingOwnVolumeEvents = _switchingToVolumeEventsHandler.HasSwitched;
        var isOwnVolume = mapping.Type is MappingType.CloudFiles or MappingType.HostDeviceFolder or MappingType.ForeignDevice;
        var nodeId = mapping.Remote.RootItemType is LinkType.Folder
            ? mapping.Remote.RootLinkId ?? throw new InvalidOperationException()
            : RootPropertyProvider.GetVirtualRootFolderId(mapping.Id);

        return new RootInfo<string>(
            Id: mapping.Id,
            VolumeId: mapping.Remote.InternalVolumeId,
            nodeId)
        {
            // Remote events are retrieved per volume, remote InternalVolumeId serves as an event scope.
            // Except for own volume if switching to volume events has not succeeded,
            // then remote ShareId serves as an event scope.
            EventScope = (isOwnVolume && !isUsingOwnVolumeEvents
                    ? mapping.Remote.ShareId
                    : RootPropertyProvider.GetEventScope(mapping.Remote.InternalVolumeId))
                ?? throw new InvalidOperationException(),

            // Moving between local sync folders is not currently supported
            MoveScope = mapping.Id,
            LocalPath = mapping.Remote.RootItemType is LinkType.File
                ? Path.GetDirectoryName(mapping.Local.Path.AsSpan()).ToString()
                : mapping.Local.Path,
            IsEnabled = mapping.HasSetupSucceeded,
        };
    }

    private IFileSystemClient<string> CreateOwnVolumeClientForShare(
        string volumeId,
        string shareId)
    {
        var parameters = new FileSystemClientParameters(volumeId, shareId);

        var undecoratedClient = _undecoratedClientFactory.CreateClient(parameters);

        return new RemoteSpaceCheckingFileSystemClientDecorator(_userService, new StorageReservationHandler(), undecoratedClient);
    }

    private IFileSystemClient<string> CreateClientForSharedWithMeItem(RemoteToLocalMapping mapping)
    {
        var volumeId = mapping.Remote.VolumeId ?? throw new InvalidOperationException("Remote volume ID is not specified");
        var shareId = mapping.Remote.ShareId ?? throw new InvalidOperationException("Remote share ID is not specified");

        var undecoratedClient = mapping.Remote.RootItemType is LinkType.File
            ? CreateUndecoratedClientForFile()
            : CreateUndecoratedClientForFolder();

        IFileSystemClient<string> decoratedClient = mapping.Remote.IsReadOnly
            ? new ReadOnlyFileSystemClientDecorator(undecoratedClient)
            : undecoratedClient;

        if (mapping.Remote.RootItemType is LinkType.File)
        {
            decoratedClient = new FilteringSingleFileFileSystemClientDecorator(decoratedClient);
        }

        return decoratedClient;

        IFileSystemClient<string> CreateUndecoratedClientForFile()
        {
            var virtualFolderId = RootPropertyProvider.GetVirtualRootFolderId(mapping.Id);
            var linkId = mapping.Remote.RootLinkId;
            var linkName = Path.GetFileName(mapping.Local.Path.AsSpan()).ToString();
            var parameters = new FileSystemClientParameters(volumeId, shareId, virtualFolderId, linkId, linkName);

            return _undecoratedClientFactory.CreateClient(parameters);
        }

        IFileSystemClient<string> CreateUndecoratedClientForFolder()
        {
            return _undecoratedClientFactory.CreateClient(new FileSystemClientParameters(volumeId, shareId));
        }
    }
}
