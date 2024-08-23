using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Account;
using ProtonDrive.App.Settings;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Sync.Adapter;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Remote;

internal sealed class RemoteDecoratedFileSystemClientFactory
{
    private readonly Func<FileSystemClientParameters, IFileSystemClient<string>> _undecoratedClientFactory;
    private readonly IUserService _userService;
    private readonly ISwitchingToVolumeEventsHandler _switchingToVolumeEventsHandler;
    private readonly ILoggerFactory _loggerFactory;

    public RemoteDecoratedFileSystemClientFactory(
        Func<FileSystemClientParameters, IFileSystemClient<string>> undecoratedClientFactory,
        IUserService userService,
        ISwitchingToVolumeEventsHandler switchingToVolumeEventsHandler,
        ILoggerFactory loggerFactory)
    {
        _undecoratedClientFactory = undecoratedClientFactory;
        _userService = userService;
        _switchingToVolumeEventsHandler = switchingToVolumeEventsHandler;
        _loggerFactory = loggerFactory;
    }

    public IFileSystemClient<string> GetClient(
        IReadOnlyCollection<RemoteToLocalMapping> mappings,
        IRevisionUploadAttemptRepository revisionUploadAttemptRepository)
    {
        // A dummy (offline) client is created for non-successfully set up mappings.
        // If all mappings on the same remote share are non-successfully set up,
        // the remote file system client for share is still created, but gets abandoned.
        var rootToClientMap =
            GetOwnVolumeRootToClientMap(mappings, revisionUploadAttemptRepository)
                .Concat(GetForeignVolumeRootToClientMap(mappings, revisionUploadAttemptRepository))
                .ToDictionary(x => x.Root, x => x.Client);

        return
            new LoggingFileSystemClientDecorator<string>(
                _loggerFactory.CreateLogger<LoggingFileSystemClientDecorator<string>>(),
                new DispatchingFileSystemClient<string>(rootToClientMap));
    }

    private static IFileSystemClient<string> CreateClientForMapping(RemoteToLocalMapping mapping, IFileSystemClient<string> clientForShare)
    {
        return mapping.HasSetupSucceeded
            ? new RootedFileSystemClientDecorator<string>(new RemoteRootDirectory(mapping), null, clientForShare)
            : new OfflineFileSystemClient<string>();
    }

    private IEnumerable<(RootInfo<string> Root, IFileSystemClient<string> Client)> GetOwnVolumeRootToClientMap(
        IEnumerable<RemoteToLocalMapping> mappings,
        IRevisionUploadAttemptRepository revisionUploadAttemptRepository)
    {
        return from mapping in mappings
               where mapping.Type is MappingType.CloudFiles or MappingType.ForeignDevice or MappingType.HostDeviceFolder
               group mapping by (mapping.Remote.VolumeId, mapping.Remote.ShareId) into shareMappings
               let shareClient = CreateOwnVolumeClientForShare(shareMappings.Key.VolumeId, shareMappings.Key.ShareId, revisionUploadAttemptRepository)
               from shareMapping in shareMappings
               select (
                   Root: CreateRoot(shareMapping),
                   Client: CreateClientForMapping(shareMapping, shareClient));
    }

    private IEnumerable<(RootInfo<string> Root, IFileSystemClient<string> Client)> GetForeignVolumeRootToClientMap(
        IReadOnlyCollection<RemoteToLocalMapping> mappings,
        IRevisionUploadAttemptRepository revisionUploadAttemptRepository)
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
               let client = CreateClientForSharedWithMeItem(mapping, revisionUploadAttemptRepository)
               select (
                   Root: CreateRoot(mapping),
                   Client: CreateClientForMapping(mapping, client));
    }

    private RootInfo<string> CreateRoot(RemoteToLocalMapping shareMapping)
    {
        var isUsingOwnVolumeEvents = _switchingToVolumeEventsHandler.HasSwitched;
        var isOwnVolume = shareMapping.Type is MappingType.CloudFiles or MappingType.HostDeviceFolder or MappingType.ForeignDevice;
        var nodeId = shareMapping.Remote.RootLinkType is LinkType.Folder
            ? shareMapping.Remote.RootLinkId ?? throw new InvalidOperationException()
            : "virtual_" + shareMapping.Id;

        return new RootInfo<string>(
            Id: shareMapping.Id,
            VolumeId: shareMapping.Remote.InternalVolumeId,
            nodeId)
        {
            // Remote events are retrieved per volume, remote VolumeId serves as an event scope.
            // Except for own volume if switching to volume events has not succeeded,
            // then remote ShareId serves as an event scope.
            EventScope = (isOwnVolume && !isUsingOwnVolumeEvents
                    ? shareMapping.Remote.ShareId
                    : shareMapping.Remote.VolumeId)
                ?? throw new InvalidOperationException(),

            // Moving between local sync folders is not currently supported
            MoveScope = shareMapping.Id,
            LocalPath = shareMapping.Local.RootFolderPath,
            IsEnabled = shareMapping.HasSetupSucceeded,
        };
    }

    private IFileSystemClient<string> CreateOwnVolumeClientForShare(
        string volumeId,
        string shareId,
        IRevisionUploadAttemptRepository revisionUploadAttemptRepository)
    {
        var parameters = new FileSystemClientParameters(volumeId, shareId);

        return new RemoteSpaceCheckingFileSystemClientDecorator(
            _userService,
            new StorageReservationHandler(),
            new DraftCleaningFileSystemClientDecorator(
                revisionUploadAttemptRepository,
                CreateUndecoratedClient(parameters)));
    }

    private IFileSystemClient<string> CreateClientForSharedWithMeItem(
        RemoteToLocalMapping mapping,
        IRevisionUploadAttemptRepository revisionUploadAttemptRepository)
    {
        var volumeId = mapping.Remote.VolumeId ?? throw new InvalidOperationException("Remote volume ID is not specified");
        var shareId = mapping.Remote.ShareId ?? throw new InvalidOperationException("Remote share ID is not specified");

        if (mapping.Remote.RootLinkType is LinkType.File)
        {
            var virtualParentId = "virtual_" + mapping.Id;
            var linkId = mapping.Remote.RootLinkId;
            var linkName = mapping.Remote.RootFolderName;

            var parameters = new FileSystemClientParameters(volumeId, shareId, virtualParentId, linkId, linkName);

            return new FilteringSingleFileFileSystemClientDecorator(
                new DraftCleaningFileSystemClientDecorator(revisionUploadAttemptRepository, CreateUndecoratedClient(parameters)));
        }

        return new DraftCleaningFileSystemClientDecorator(
            revisionUploadAttemptRepository,
            CreateUndecoratedClient(new FileSystemClientParameters(volumeId, shareId)));
    }

    private IFileSystemClient<string> CreateUndecoratedClient(FileSystemClientParameters parameters)
    {
        return _undecoratedClientFactory.Invoke(parameters);
    }
}
