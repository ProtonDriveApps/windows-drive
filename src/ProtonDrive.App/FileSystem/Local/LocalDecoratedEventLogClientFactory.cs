using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Settings;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Sync.Adapter;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Windows.FileSystem.Client;

namespace ProtonDrive.App.FileSystem.Local;

internal sealed class LocalDecoratedEventLogClientFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Func<IEventLogClientEntriesFilter, IRootableEventLogClient<long>> _undecoratedClientFactory;
    private readonly LocalRootMapForDeletionDetectionFactory _rootMapForDeletionDetectionFactory;
    private readonly IFileTransferAbortionStrategy<long> _fileTransferAbortionStrategy;
    private readonly IRootDeletionHandler _rootDeletionHandler;

    public LocalDecoratedEventLogClientFactory(
        ILoggerFactory loggerFactory,
        Func<IEventLogClientEntriesFilter, IRootableEventLogClient<long>> undecoratedClientFactory,
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
        var sharedWithMeRootFolderPath = mappings
            .Where(x => x.Type is MappingType.SharedWithMeRootFolder)
            .Select(x => x.Local.RootFolderPath)
            .FirstOrDefault();

        var sharedWithMeItemPaths = mappings
            .Where(x => x.Type is MappingType.SharedWithMeItem && x.Remote.RootFolderName is not null)
            .Select(x => x.Remote.RootLinkType is LinkType.Folder
                ? x.Local.RootFolderPath
                : Path.Combine(x.Local.RootFolderPath, x.Remote.RootFolderName!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IEventLogClientEntriesFilter sharedWithMeRootFolderEntriesFilter = sharedWithMeRootFolderPath is not null
            ? new SharedWithMeRootFolderEntriesFilter(sharedWithMeRootFolderPath, sharedWithMeItemPaths)
            : NullEventLogClientEntriesFilter.Instance;

        // Based on cloud files mapping, a local event log client is created on account root folder.
        // It also covers foreign device root folders and shared with me items, if any.
        // A local event log client is created per host device folder.
        // Not successfully set up mappings are excluded.
        var eventScopeToClientMap = mappings
            .Where(m => m is { HasSetupSucceeded: true, Type: MappingType.CloudFiles or MappingType.HostDeviceFolder })
            .ToDictionary(
                m => m.Id.ToString(),
                m => (VolumeId: m.Local.InternalVolumeId, Client: CreateClientForMapping(m, sharedWithMeRootFolderEntriesFilter)));

        var (volumeId, nodeIdToRootMap) = _rootMapForDeletionDetectionFactory.Create(mappings);

        var sharedWithMeFileNameToMappingIdMaps = new SharedWithMeFileRootDirectoryMaps<long>(mappings
            .Where(x => x.Type is MappingType.SharedWithMeItem && x.Remote.RootLinkType is LinkType.File && x.Remote.RootFolderName is not null)
            .Select(x => new { FileName = x.Remote.RootFolderName!, MappingId = x.Id })
            .ToDictionary(x => x.FileName, x => x.MappingId)
            .AsReadOnly());

        return
            new RootDeletionDetectingEventLogClientDecorator<long>(
                new RootDeletionDetector<long>(_rootDeletionHandler, volumeId, nodeIdToRootMap),
                new FileTransferAbortingEventLogClientDecorator(
                    new AggregatingEventLogClient<long>(eventScopeToClientMap, sharedWithMeFileNameToMappingIdMaps),
                    _fileTransferAbortionStrategy));
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
                return new LocalRootDirectory(accountRootFolderPath, id: default);

            case MappingType.HostDeviceFolder:
                return new LocalRootDirectory(mapping.Local);

            default:
                throw new InvalidEnumArgumentException(nameof(mapping.Type), (int)mapping.Type, typeof(MappingType));
        }
    }

    private IEventLogClient<long> CreateClientForMapping(RemoteToLocalMapping mapping, IEventLogClientEntriesFilter eventLogEntriesFilter)
    {
        var logger = _loggerFactory.CreateLogger<LoggingEventLogClientDecorator<long>>();

        var rootedClient = new RootedEventLogClientDecorator<long>(
            _loggerFactory.CreateLogger<RootedEventLogClientDecorator<long>>(),
            CreateRootDirectory(mapping),
            CreateUndecoratedClient(eventLogEntriesFilter));

        return new LoggingEventLogClientDecorator<long>(logger, rootedClient, mapping.Local.InternalVolumeId, scope: mapping.Id.ToString());
    }

    private IRootableEventLogClient<long> CreateUndecoratedClient(IEventLogClientEntriesFilter eventLogEntriesFilter)
    {
        return _undecoratedClientFactory.Invoke(eventLogEntriesFilter);
    }
}
