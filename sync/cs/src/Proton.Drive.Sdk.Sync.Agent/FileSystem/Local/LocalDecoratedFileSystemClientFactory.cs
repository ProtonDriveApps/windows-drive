using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Adapter;
using Proton.Drive.Sdk.Sync.Agent.FileSystem.Local.SpecialFolders;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Local;

internal sealed class LocalDecoratedFileSystemClientFactory
{
    private readonly ILocalVolumeInfoProvider _volumeInfoProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILocalFileSystemClientFactory _undecoratedClientFactory;
    private readonly IFileTransferAbortionStrategy<long> _fileTransferAbortionStrategy;
    private readonly ISyncFolderStructureProtector _folderStructureProtector;

    public LocalDecoratedFileSystemClientFactory(
        ILocalVolumeInfoProvider volumeInfoProvider,
        ILoggerFactory loggerFactory,
        ILocalFileSystemClientFactory undecoratedClientFactory,
        IFileTransferAbortionStrategy<long> fileTransferAbortionStrategy,
        ISyncFolderStructureProtector folderStructureProtector)
    {
        _volumeInfoProvider = volumeInfoProvider;
        _loggerFactory = loggerFactory;
        _undecoratedClientFactory = undecoratedClientFactory;
        _fileTransferAbortionStrategy = fileTransferAbortionStrategy;
        _folderStructureProtector = folderStructureProtector;
    }

    public IFileSystemClient<long> GetClient(IReadOnlyCollection<RemoteToLocalMapping> mappings, LocalAdapterSettings settings)
    {
        var cloudFilesMapping = mappings.FirstOrDefault(m => m.Type is MappingType.CloudFiles);

        // A local file system client is created per sync folder mapping
        var rootClientPairs = mappings
            .Where(m => m.Type is MappingType.CloudFiles or MappingType.ForeignDevice or MappingType.HostDeviceFolder)
            .Select(m => (
                Root: CreateRoot(m),
                Client: CreateClientForMapping(m, settings)));

        var sharedWithMeRootFolderMapping = mappings.FirstOrDefault(m => m.Type is MappingType.SharedWithMeRootFolder);

        // A single local file system client is created for all "shared with me" items.
        // An aggregating decorator aggregates requests from multiple sync roots
        // and dispatches file hydration demand to sync roots.
        var aggregatingClient = sharedWithMeRootFolderMapping is not null
            ? new AggregatingFileSystemClientDecorator<long>(
                new LocalRootDirectory(sharedWithMeRootFolderMapping.Local),
                CreateUndecoratedClient(sharedWithMeRootFolderMapping.SyncMethod))
            : null;

        var sharedWithMeRootToClientMap = mappings
            .Where(m => m.Type is MappingType.SharedWithMeItem)
            .Select(m => (
                Root: CreateRoot(m, sharedWithMeRootFolderMapping),
                Client: CreateClientForSharedWithMeItemMapping(m, parentMapping: sharedWithMeRootFolderMapping, settings, aggregatingClient)));

        var rootToClientMap = rootClientPairs.Concat(sharedWithMeRootToClientMap).ToDictionary(x => x.Root, x => x.Client);

        return
            new LoggingFileSystemClientDecorator<long>(
                _loggerFactory.CreateLogger<LoggingFileSystemClientDecorator<long>>(),
                new DispatchingFileSystemClient<long>(rootToClientMap));

        RootInfo<long> CreateRoot(RemoteToLocalMapping mapping, RemoteToLocalMapping? parentMapping = null)
        {
            var volumeId = mapping.Local.InternalVolumeId;

            var rootFolderId = mapping.Type is MappingType.SharedWithMeItem && mapping.Remote.RootItemType is LinkType.File
                ? parentMapping?.Local.RootFolderId ?? throw new InvalidOperationException()
                : mapping.Local.RootFolderId;

            return new RootInfo<long>(
                Id: mapping.Id,
                volumeId,
                NodeId: rootFolderId)
            {
                EventScope = GetEventScope(mapping),
                MoveScope = GetMoveScope(mapping.Remote),
                IsOnDemand = mapping.SyncMethod is SyncMethod.OnDemand,
                LocalPath = GetLocalPath(mapping),
                IsEnabled = mapping.HasSetupSucceeded,
            };
        }

        string GetEventScope(RemoteToLocalMapping mapping)
        {
            // Local events of host device folders are monitored on each host device folder using mapping ID as an event scope
            if (mapping.Type is MappingType.HostDeviceFolder)
            {
                return mapping.Id.ToString();
            }

            // The rest of events are monitored on the account root folder using cloud files mapping ID as an event scope.
            return (cloudFilesMapping?.Id ?? 0).ToString();
        }

        int GetMoveScope(RemoteReplica replica)
        {
            // Moving between remote shares is supported by the API without requiring to provide the destination Share ID.
            // Moving between remote volumes is not supported.
            return replica.InternalVolumeId;
        }

        string GetLocalPath(RemoteToLocalMapping mapping)
        {
            return mapping.Remote.RootItemType is LinkType.File
                ? GetVirtualRootPath(mapping)
                : mapping.Local.Path;
        }

        string GetVirtualRootPath(RemoteToLocalMapping mapping)
        {
            // Every shared with me file is placed in a virtual root folder
            return Path.GetDirectoryName(mapping.Local.Path.AsSpan()).ToString();
        }
    }

    private IFileSystemClient<long> CreateClientForMapping(RemoteToLocalMapping mapping, LocalAdapterSettings settings)
    {
        // A dummy (offline) client is created for non-successfully set up mappings
        return mapping.HasSetupSucceeded
            ? CreateClientForMapping(mapping.SyncMethod, mapping, settings)
            : new OfflineFileSystemClient<long>();
    }

    private IFileSystemClient<long> CreateClientForSharedWithMeItemMapping(
        RemoteToLocalMapping mapping,
        RemoteToLocalMapping? parentMapping,
        LocalAdapterSettings settings,
        IFileSystemClient<long>? aggregatingClient)
    {
        if (!mapping.HasSetupSucceeded || parentMapping == null || aggregatingClient == null)
        {
            // A dummy (offline) client is created for non-successfully set up mappings
            // and when the shared with me root folder (parent) mapping is non successfully setup
            return new OfflineFileSystemClient<long>();
        }

        return mapping.Remote.RootItemType switch
        {
            LinkType.Folder when !mapping.Remote.IsReadOnly => CreateClientForSharedWithMeWritableFolderMapping(mapping, settings, aggregatingClient),
            LinkType.Folder when mapping.Remote.IsReadOnly => CreateClientForSharedWithMeReadOnlyFolderMapping(mapping, settings, aggregatingClient),
            LinkType.File when !mapping.Remote.IsReadOnly => CreateClientForSharedWithMeWritableFileMapping(mapping, parentMapping, settings, aggregatingClient),
            LinkType.File when mapping.Remote.IsReadOnly => CreateClientForSharedWithMeReadOnlyFileMapping(mapping, parentMapping, aggregatingClient),
            _ => throw new InvalidEnumArgumentException(nameof(mapping.Remote.RootItemType), (int)mapping.Remote.RootItemType, typeof(LinkType)),
        };
    }

    private IFileSystemClient<long> CreateClientForSharedWithMeWritableFolderMapping(
        RemoteToLocalMapping mapping,
        LocalAdapterSettings settings,
        IFileSystemClient<long> aggregatingClient)
    {
        var rootedClient = new RootedFileSystemClientDecorator(
            new LocalRootDirectory(mapping.Local),
            aggregatingClient);

        return AddCommonDecorators(mapping, settings, rootedClient);
    }

    private IFileSystemClient<long> CreateClientForSharedWithMeReadOnlyFolderMapping(
        RemoteToLocalMapping mapping,
        LocalAdapterSettings settings,
        IFileSystemClient<long> aggregatingClient)
    {
        var protectingClient = new ProtectingFolderFileSystemClientDecorator(_folderStructureProtector, aggregatingClient);

        var rootedClient = new RootedFileSystemClientDecorator(
            new LocalRootDirectory(mapping.Local),
            protectingClient);

        var readOnlyClient = new ReadOnlyFileSystemClientDecorator(rootedClient);

        return AddCommonDecorators(mapping, settings, readOnlyClient);
    }

    private IFileSystemClient<long> CreateClientForSharedWithMeWritableFileMapping(
        RemoteToLocalMapping mapping,
        RemoteToLocalMapping parentMapping,
        LocalAdapterSettings settings,
        IFileSystemClient<long> aggregatingClient)
    {
        var localFileName = Path.GetFileName(mapping.Local.Path);
        var parentFolderId = parentMapping.Local.RootFolderId;
        var parentFolderPath = parentMapping.Local.Path;

        Ensure.NotNullOrEmpty(localFileName, nameof(localFileName));

        var rootedClient = new RootedFileSystemClientDecorator(
            new LocalRootDirectory(parentFolderPath, parentFolderId),
            aggregatingClient);

        var virtualRootClient = new VirtualFileRootFileSystemClientDecorator(
            parentFolderId,
            localFileName,
            rootedClient);

        var backingUpClient = new BackingUpFileSystemClientDecorator<long>(
            _loggerFactory.CreateLogger<BackingUpFileSystemClientDecorator<long>>(),
            new FileNameFactory<long>(settings.EditConflictNamePattern),
            virtualRootClient);

        var transferAbortionClient = new TransferAbortionCapableFileSystemClientDecorator<long>(
            _fileTransferAbortionStrategy,
            backingUpClient);

        return transferAbortionClient;
    }

    private IFileSystemClient<long> CreateClientForSharedWithMeReadOnlyFileMapping(
        RemoteToLocalMapping mapping,
        RemoteToLocalMapping parentMapping,
        IFileSystemClient<long> aggregatingClient)
    {
        var localFileName = Path.GetFileName(mapping.Local.Path);
        var parentFolderId = parentMapping.Local.RootFolderId;
        var parentFolderPath = parentMapping.Local.Path;

        Ensure.NotNullOrEmpty(localFileName, nameof(localFileName));

        var protectingClient = new ProtectingFileFileSystemClientDecorator(_folderStructureProtector, aggregatingClient);

        var rootedClient = new RootedFileSystemClientDecorator(
            new LocalRootDirectory(parentFolderPath, parentFolderId),
            protectingClient);

        var readOnlyClient = new ReadOnlyFileSystemClientDecorator(rootedClient);

        var virtualRootClient = new VirtualFileRootFileSystemClientDecorator(
            parentFolderId,
            localFileName,
            readOnlyClient);

        var transferAbortionClient = new TransferAbortionCapableFileSystemClientDecorator<long>(
            _fileTransferAbortionStrategy,
            virtualRootClient);

        return transferAbortionClient;
    }

    private IFileSystemClient<long> CreateClientForMapping(SyncMethod syncMethod, RemoteToLocalMapping mapping, LocalAdapterSettings settings)
    {
        var localRootDirectory = new LocalRootDirectory(mapping.Local);

        if (!_volumeInfoProvider.IsNtfsFileSystem(localRootDirectory.Path))
        {
            throw new InvalidFileSystemException("File system is not NTFS");
        }

        var client = syncMethod switch
        {
            SyncMethod.Classic => new RootedFileSystemClientDecorator(
                localRootDirectory,
                new LocalSpaceCheckingFileSystemClientDecorator<long>(
                    localRootDirectory.Path,
                    _volumeInfoProvider,
                    CreateUndecoratedClient(syncMethod))),

            // Local volume space checking decorator is not needed when using
            // the on-demand hydration file system client
            SyncMethod.OnDemand => new RootedFileSystemClientDecorator(
                localRootDirectory,
                CreateUndecoratedClient(syncMethod)),

            _ => throw new InvalidEnumArgumentException(nameof(syncMethod), (int)syncMethod, typeof(SyncMethod)),
        };

        return AddCommonDecorators(mapping, settings, client);
    }

    private IFileSystemClient<long> AddCommonDecorators(RemoteToLocalMapping mapping, LocalAdapterSettings settings, IFileSystemClient<long> client)
    {
        var rootFolder = new SpecialRootFolder<long>(mapping.Local.RootFolderId);

        var localTempFolder = new HiddenSpecialFolder<long>(
            settings.TempFolderName,
            rootFolder,
            client,
            _loggerFactory.CreateLogger<HiddenSpecialFolder<long>>());

        var localTrashFolder = new LocalTrash<long>(
            settings.TrashFolderName,
            localTempFolder,
            client,
            new ThreadPoolScheduler(),
            TimeSpan.FromMinutes(5),
            _loggerFactory.CreateLogger<LocalTrash<long>>());

        if (!mapping.Remote.IsReadOnly)
        {
            client = new BackingUpFileSystemClientDecorator<long>(
                _loggerFactory.CreateLogger<BackingUpFileSystemClientDecorator<long>>(),
                new FileNameFactory<long>(settings.EditConflictNamePattern),
                client);
        }

        var deletionFallbackClient = new PermanentDeletionFallbackFileSystemClientDecorator<long>(
            _loggerFactory.CreateLogger<PermanentDeletionFallbackFileSystemClientDecorator<long>>(),
            localTrashFolder,
            new FileNameFactory<long>(settings.DeletedNamePattern),
            client);

        var transferAbortionClient = new TransferAbortionCapableFileSystemClientDecorator<long>(
            _fileTransferAbortionStrategy,
            deletionFallbackClient);

        return transferAbortionClient;
    }

    private IFileSystemClient<long> CreateUndecoratedClient(SyncMethod syncMethod)
    {
        return syncMethod switch
        {
            SyncMethod.Classic => CreateUndecoratedClassicClient(),
            SyncMethod.OnDemand => CreateUndecoratedOnDemandHydrationClient(),
            _ => throw new InvalidEnumArgumentException(nameof(syncMethod), (int)syncMethod, typeof(SyncMethod)),
        };
    }

    private IFileSystemClient<long> CreateUndecoratedClassicClient()
    {
        return _undecoratedClientFactory.CreateClassicClient();
    }

    private IFileSystemClient<long> CreateUndecoratedOnDemandHydrationClient()
    {
        return _undecoratedClientFactory.CreateOnDemandHydrationClient();
    }
}
