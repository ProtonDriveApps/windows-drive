using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Mapping.Setup;
using ProtonDrive.App.Services;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;
using SharedWithMeItem = ProtonDrive.Client.Shares.SharedWithMe.SharedWithMeItem;

namespace ProtonDrive.App.Mapping;

internal sealed class SharedWithMeMappingService : ISharedWithMeMappingService, IStoppableService, IMappingsAware
{
    private readonly ISyncFolderPathProvider _syncFolderPathProvider;
    private readonly ILocalFolderService _localFolderService;
    private readonly IMappingRegistry _mappingRegistry;
    private readonly ILogger<SharedWithMeMappingService> _logger;

    private readonly CoalescingAction _mappingMaintenance;

    private volatile bool _stopping;
    private bool _mappingsModificationIsInProgress;

    public SharedWithMeMappingService(
        ISyncFolderPathProvider syncFolderPathProvider,
        ILocalFolderService localFolderService,
        IMappingRegistry mappingRegistry,
        ILogger<SharedWithMeMappingService> logger)
    {
        _syncFolderPathProvider = syncFolderPathProvider;
        _localFolderService = localFolderService;
        _mappingRegistry = mappingRegistry;
        _logger = logger;

        _mappingMaintenance = _logger.GetCoalescingActionWithExceptionsLoggingAndCancellationHandling(MaintainMappingsAsync, nameof(SharedWithMeMappingService));
    }

    Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _stopping = true;
        _mappingMaintenance.Cancel();

        return WaitForCompletionAsync();
    }

    void IMappingsAware.OnMappingsChanged(
        IReadOnlyCollection<RemoteToLocalMapping> activeMappings,
        IReadOnlyCollection<RemoteToLocalMapping> deletedMappings)
    {
        if (_mappingsModificationIsInProgress)
        {
            return;
        }

        ScheduleMappingMaintenance();
    }

    public async Task AddSharedWithMeItemAsync(SharedWithMeItem item, CancellationToken cancellationToken)
    {
        Ensure.IsFalse(item.IsReadOnly, "Must be non read-only", nameof(item));
        Ensure.NotNullOrEmpty(item.Id, nameof(item), nameof(item.Id));
        Ensure.NotNullOrEmpty(item.LinkId, nameof(item), nameof(item.LinkId));
        Ensure.NotNullOrEmpty(item.VolumeId, nameof(item), nameof(item.VolumeId));
        Ensure.NotNullOrEmpty(item.Name, nameof(item), nameof(item.Name));

        var nameToLog = _logger.GetSensitiveValueForLogging(item.Name);
        _logger.LogInformation("Requested to add shared with me folder \"{Name}\"", nameToLog);

        using var mappings = await _mappingRegistry.GetMappingsAsync(cancellationToken).ConfigureAwait(false);

        var activeMappings = mappings.GetActive();

        if (activeMappings.Any(m => IsMappingOfSharedWithMeItem(m, item)))
        {
            _logger.LogInformation("Ignored shared with me folder \"{Name}\", since it is already mapped", nameToLog);
        }

        var itemsPath = _syncFolderPathProvider.GetSharedWithMeItemsFolderPath();

        if (string.IsNullOrEmpty(itemsPath))
        {
            _logger.LogWarning("Cannot obtain shared with me items path, possibly due to account root folder is not defined");

            return;
        }

        if (!activeMappings.Any(IsSharedWithMeItemsFolderMapping))
        {
            mappings.Add(CreateSharedWithMeItemsFolderMapping(itemsPath));
        }

        var namesInUse = activeMappings
            .Where(IsSharedWithMeItemMapping)
            .Select(GetLocalName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var uniqueName = GetUniqueName(item.Name, namesInUse, itemsPath, item.IsFolder);

        mappings.Add(CreateSharedWithMeItemMapping(item, uniqueName, itemsPath));

        Save(mappings);
    }

    public async Task RemoveSharedWithMeItemAsync(SharedWithMeItem item, CancellationToken cancellationToken)
    {
        using var mappings = await _mappingRegistry.GetMappingsAsync(cancellationToken).ConfigureAwait(false);

        var nameToLog = _logger.GetSensitiveValueForLogging(item.Name);
        _logger.LogInformation(
            "Requested to remove shared with me folder \"{Name}\" (remote volume ID={VolumeId}, link ID={LinkId}, share ID={ShareId})",
            nameToLog,
            item.VolumeId,
            item.LinkId,
            item.Id);

        var mapping = mappings.GetActive().FirstOrDefault(m => IsMappingOfSharedWithMeItem(m, item));

        if (mapping is null)
        {
            _logger.LogWarning("Unable to find mapping for shared with me sync folder \"{Name}\"", nameToLog);

            return;
        }

        mappings.Delete(mapping);

        TryDeleteSharedWithMeItemsFolderMapping(mappings);

        Save(mappings);
    }

    internal Task WaitForCompletionAsync()
    {
        // Wait for all scheduled tasks to complete
        return _mappingMaintenance.CurrentTask;
    }

    private static RemoteToLocalMapping CreateSharedWithMeItemsFolderMapping(string folderPath)
    {
        return new RemoteToLocalMapping
        {
            Type = MappingType.SharedWithMeRootFolder,
            SyncMethod = SyncMethod.OnDemand,
            Local =
            {
                RootFolderPath = folderPath,
            },
        };
    }

    private static RemoteToLocalMapping CreateSharedWithMeItemMapping(SharedWithMeItem item, string localFolderName, string sharedWithMeRootPath)
    {
        return new RemoteToLocalMapping
        {
            Type = MappingType.SharedWithMeItem,
            SyncMethod = SyncMethod.OnDemand,
            Local =
            {
                RootFolderPath = item.IsFolder ? Path.Combine(sharedWithMeRootPath, localFolderName) : sharedWithMeRootPath,
            },
            Remote =
            {
                ShareId = item.Id,
                RootLinkId = item.LinkId,
                VolumeId = item.VolumeId,
                RootFolderName = localFolderName,
                RootLinkType = item.IsFolder ? LinkType.Folder : LinkType.File,
            },
        };
    }

    private static RemoteToLocalMapping CreateSharedWithMeItemMapping(RemoteToLocalMapping mapping, string uniqueName, string sharedWithMeRootPath)
    {
        return new RemoteToLocalMapping
        {
            Type = mapping.Type,
            SyncMethod = SyncMethod.OnDemand,
            Local =
            {
                RootFolderPath = Path.Combine(sharedWithMeRootPath, uniqueName),
            },
            Remote = new RemoteReplica
            {
                RootLinkId = mapping.Remote.RootLinkId,
                ShareId = mapping.Remote.ShareId,
                VolumeId = mapping.Remote.VolumeId,
                RootFolderName = mapping.Remote.RootFolderName,
                IsReadOnly = mapping.Remote.IsReadOnly,
                RootLinkType = mapping.Remote.RootLinkType,
            },
        };
    }

    private static bool IsMappingOfSharedWithMeItem(RemoteToLocalMapping mapping, SharedWithMeItem item)
    {
        return IsSharedWithMeItemMapping(mapping) &&
            ((mapping.Remote.RootLinkId == item.LinkId &&
                    mapping.Remote.VolumeId == item.VolumeId) ||
                (mapping.Remote.ShareId == item.Id));
    }

    private static bool IsSharedWithMeMapping(RemoteToLocalMapping mapping) => IsSharedWithMeItemsFolderMapping(mapping) || IsSharedWithMeItemMapping(mapping);
    private static bool IsSharedWithMeItemsFolderMapping(RemoteToLocalMapping mapping) => mapping.Type is MappingType.SharedWithMeRootFolder;
    private static bool IsSharedWithMeItemMapping(RemoteToLocalMapping mapping) => mapping.Type is MappingType.SharedWithMeItem;

    private static string GetLocalName(RemoteToLocalMapping mapping)
    {
        return Path.GetFileName(mapping.Local.RootFolderPath);
    }

    private string GetUniqueName(string name, ISet<string> namesInUse, string parentPath, bool isFolder)
    {
        var nameGenerator = new NumberSuffixedNameGenerator(name, isFolder ? NameType.Folder : NameType.File);

        var uniqueName = nameGenerator.GenerateNames().First(
            candidateName =>
            {
                var itemPath = Path.Combine(parentPath, candidateName);
                return !namesInUse.Contains(candidateName) &&
                       !_localFolderService.FolderExists(itemPath) &&
                       !_localFolderService.FileExists(itemPath);
            });

        namesInUse.Add(uniqueName);

        return uniqueName;
    }

    private void ScheduleMappingMaintenance()
    {
        if (_stopping)
        {
            return;
        }

        _mappingMaintenance.Cancel();
        _mappingMaintenance.Run();
    }

    private async Task MaintainMappingsAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Started maintaining shared with me mappings");

        using var mappings = await _mappingRegistry.GetMappingsAsync(cancellationToken).ConfigureAwait(false);

        var activeMappings = mappings.GetActive();

        var cloudFilesMapping = activeMappings.FirstOrDefault(m => m.Type is MappingType.CloudFiles);
        var sharedWithMeItemsFolderPath = _syncFolderPathProvider.GetSharedWithMeItemsFolderPath();

        if (cloudFilesMapping == null || string.IsNullOrEmpty(sharedWithMeItemsFolderPath))
        {
            return;
        }

        var numberOfHandledMappings = 0;

        var namesInUse = activeMappings
            .Where(IsSharedWithMeItemMapping)
            .Where(m => m.Id > cloudFilesMapping.Id)
            .Select(GetLocalName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in activeMappings.Where(IsSharedWithMeMapping).Where(m => m.Id < cloudFilesMapping.Id))
        {
            var newMapping = CreateMapping(mapping);

            mappings.Delete(mapping);
            mappings.Add(newMapping);
            numberOfHandledMappings++;
        }

        Save(mappings);

        _logger.LogInformation(
            "Finished maintaining shared with me mappings: {NumberOfAddedMapping} added, {NumberOfDeletedMappings} deleted",
            numberOfHandledMappings,
            numberOfHandledMappings);

        return;

        RemoteToLocalMapping CreateMapping(RemoteToLocalMapping mapping)
        {
            if (mapping.Type is MappingType.SharedWithMeRootFolder)
            {
                return CreateSharedWithMeItemsFolderMapping(sharedWithMeItemsFolderPath);
            }

            var uniqueName = GetUniqueName(
                mapping.Remote.RootFolderName ?? string.Empty,
                namesInUse,
                sharedWithMeItemsFolderPath,
                mapping.Type is MappingType.SharedWithMeItem);

            return CreateSharedWithMeItemMapping(mapping, uniqueName, sharedWithMeItemsFolderPath);
        }
    }

    private void TryDeleteSharedWithMeItemsFolderMapping(IUpdatableMappings mappings)
    {
        var activeMappings = mappings.GetActive();

        if (activeMappings.Any(IsSharedWithMeItemMapping))
        {
            return;
        }

        var itemsMapping = activeMappings.FirstOrDefault(IsSharedWithMeItemsFolderMapping);

        if (itemsMapping == null)
        {
            _logger.LogWarning("Unable to find mapping for shared with me items folder");

            return;
        }

        mappings.Delete(itemsMapping);
    }

    private void Save(IUpdatableMappings mappings)
    {
        try
        {
            _mappingsModificationIsInProgress = true;

            mappings.SaveAndNotify();
        }
        finally
        {
            _mappingsModificationIsInProgress = false;
        }
    }
}
