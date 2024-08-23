using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.FileSystem.Local.SpecialFolders;
using ProtonDrive.App.FileSystem.Remote;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Shared.Volume;
using ProtonDrive.Sync.Adapter;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Local;

internal sealed class LocalDecoratedFileSystemClientFactory
{
    private readonly ILocalVolumeInfoProvider _volumeInfoProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Func<IFileSystemClient<long>> _undecoratedClassicClientFactory;
    private readonly Func<IFileSystemClient<long>> _undecoratedOnDemandHydrationClientFactory;
    private readonly IFileTransferAbortionStrategy<long> _fileTransferAbortionStrategy;

    public LocalDecoratedFileSystemClientFactory(
        ILocalVolumeInfoProvider volumeInfoProvider,
        ILoggerFactory loggerFactory,
        Func<IFileSystemClient<long>> undecoratedClassicClientFactory,
        Func<IFileSystemClient<long>> undecoratedOnDemandHydrationClientFactory,
        IFileTransferAbortionStrategy<long> fileTransferAbortionStrategy)
    {
        _volumeInfoProvider = volumeInfoProvider;
        _loggerFactory = loggerFactory;
        _undecoratedClassicClientFactory = undecoratedClassicClientFactory;
        _undecoratedOnDemandHydrationClientFactory = undecoratedOnDemandHydrationClientFactory;
        _fileTransferAbortionStrategy = fileTransferAbortionStrategy;
    }

    public IFileSystemClient<long> GetClient(IReadOnlyCollection<RemoteToLocalMapping> mappings, LocalAdapterSettings settings)
    {
        int lastMoveScope = 0;
        var shareIdToMoveScopeMap = new Dictionary<string, int>();

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
        var parentClient = sharedWithMeRootFolderMapping is not null
            ? new AggregatingFileSystemClientDecorator<long>(
                new LocalRootDirectory(sharedWithMeRootFolderMapping.Local),
                CreateUndecoratedClient(sharedWithMeRootFolderMapping.SyncMethod))
            : null;

        var sharedWithMeRootToClientMap = mappings
            .Where(m => m.Type is MappingType.SharedWithMeItem)
            .Select(m => (
                Root: CreateRoot(m),
                Client: CreateClientForSharedWithMeItemMapping(m, parentClient)));

        var rootToClientMap = rootClientPairs.Concat(sharedWithMeRootToClientMap).ToDictionary(x => x.Root, x => x.Client);

        return
            new LoggingFileSystemClientDecorator<long>(
                _loggerFactory.CreateLogger<LoggingFileSystemClientDecorator<long>>(),
                new DispatchingFileSystemClient<long>(rootToClientMap));

        RootInfo<long> CreateRoot(RemoteToLocalMapping mapping)
        {
            var volumeId = mapping.Remote.RootLinkType is LinkType.Folder
                ? mapping.Local.InternalVolumeId
                : VirtualInternalVolumeIdProvider.GetId(mapping.Local.InternalVolumeId, mapping.Id);

            return new RootInfo<long>(
                Id: mapping.Id,
                volumeId,
                NodeId: mapping.Local.RootFolderId)
            {
                EventScope = GetEventScope(mapping),
                MoveScope = GetMoveScope(mapping.Remote.ShareId),
                IsOnDemand = mapping.SyncMethod is SyncMethod.OnDemand,
                LocalPath = mapping.Local.RootFolderPath,
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

            // Local events are monitored on the account root folder using cloud files mapping ID as an event scope.
            return (cloudFilesMapping?.Id ?? 0).ToString();
        }

        int GetMoveScope(string? shareId)
        {
            Ensure.NotNull(shareId, nameof(shareId));

            // Moving between remote shares is not supported
            if (shareIdToMoveScopeMap.TryGetValue(shareId, out var moveScope))
            {
                return moveScope;
            }

            moveScope = ++lastMoveScope;
            shareIdToMoveScopeMap.Add(shareId, moveScope);

            return moveScope;
        }
    }

    private IFileSystemClient<long> CreateClientForMapping(RemoteToLocalMapping mapping, LocalAdapterSettings settings)
    {
        // A dummy (offline) client is created for non-successfully set up mappings
        return mapping.HasSetupSucceeded
            ? CreateClientForMapping(mapping.SyncMethod, mapping, settings)
            : new OfflineFileSystemClient<long>();
    }

    private IFileSystemClient<long> CreateClientForSharedWithMeItemMapping(RemoteToLocalMapping mapping, IFileSystemClient<long>? parentClient)
    {
        if (mapping is not { HasSetupSucceeded: true, Remote.RootFolderName: not null } || parentClient == null)
        {
            // A dummy (offline) client is created for non-successfully set up mappings
            // and when the shared with me items folder (parent) mapping is non successfully setup
            return new OfflineFileSystemClient<long>();
        }

        var innerClient = mapping.Remote.RootLinkType is LinkType.File
            ? new FilteringSingleFileFileSystemClientDecorator(parentClient, mapping.Remote.RootFolderName)
            : parentClient;

        return new RootedFileSystemClientDecorator<long>(
            new LocalRootDirectory(mapping.Local),
            mapping.Remote.RootLinkType is LinkType.Folder ? null : mapping.Remote.RootFolderName,
            innerClient);
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
            SyncMethod.Classic => new RootedFileSystemClientDecorator<long>(
                localRootDirectory,
                null,
                new LocalSpaceCheckingFileSystemClientDecorator<long>(
                    localRootDirectory.Path,
                    _volumeInfoProvider,
                    CreateUndecoratedClient(syncMethod))),

            // Local volume space checking decorator is not needed when using
            // the on-demand hydration file system client
            SyncMethod.OnDemand => new RootedFileSystemClientDecorator<long>(
                localRootDirectory,
                null,
                CreateUndecoratedClient(syncMethod)),

            _ => throw new InvalidEnumArgumentException(nameof(syncMethod), (int)syncMethod, typeof(SyncMethod)),
        };

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

        return
            new TransferAbortionCapableFileSystemClientDecorator<long>(
                _fileTransferAbortionStrategy,
                new PermanentDeletionFallbackFileSystemClientDecorator<long>(
                    _loggerFactory.CreateLogger<PermanentDeletionFallbackFileSystemClientDecorator<long>>(),
                    localTrashFolder,
                    new FileNameFactory<long>(settings.DeletedNamePattern),
                    new BackingUpFileSystemClientDecorator<long>(
                        _loggerFactory.CreateLogger<BackingUpFileSystemClientDecorator<long>>(),
                        new FileNameFactory<long>(settings.EditConflictNamePattern),
                        client)));
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
        return _undecoratedClassicClientFactory.Invoke();
    }

    private IFileSystemClient<long> CreateUndecoratedOnDemandHydrationClient()
    {
        return _undecoratedOnDemandHydrationClientFactory.Invoke();
    }
}
