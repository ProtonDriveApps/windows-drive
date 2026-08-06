using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Services;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Logging;
using Proton.Drive.Shared.Threading;
using SharedWithMeItem = Proton.Drive.Sdk.Sync.Client.Shares.SharedWithMe.SharedWithMeItem;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping;

internal sealed class SharedWithMeMappingService : ISharedWithMeMappingService, IStoppableService, IMappingsAware
{
    private readonly ISyncFolderPathProvider _syncFolderPathProvider;
    private readonly ILocalFolderService _localFolderService;
    private readonly IMappingRegistry _mappingRegistry;
    private readonly INumberSuffixedNameGenerator _numberSuffixedNameGenerator;
    private readonly ILogger<SharedWithMeMappingService> _logger;

    private readonly CoalescingAction _mappingMaintenance;

    private volatile bool _stopping;
    private bool _mappingsModificationIsInProgress;

    public SharedWithMeMappingService(
        ISyncFolderPathProvider syncFolderPathProvider,
        ILocalFolderService localFolderService,
        IMappingRegistry mappingRegistry,
        INumberSuffixedNameGenerator numberSuffixedNameGenerator,
        ILogger<SharedWithMeMappingService> logger)
    {
        _syncFolderPathProvider = syncFolderPathProvider;
        _localFolderService = localFolderService;
        _mappingRegistry = mappingRegistry;
        _numberSuffixedNameGenerator = numberSuffixedNameGenerator;
        _logger = logger;

        _mappingMaintenance =
            _logger.GetCoalescingActionWithExceptionsLoggingAndCancellationHandling(MaintainMappingsAsync, nameof(SharedWithMeMappingService));
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
        Ensure.NotNullOrEmpty(item.Id, nameof(item), nameof(item.Id));
        Ensure.NotNullOrEmpty(item.LinkId, nameof(item), nameof(item.LinkId));
        Ensure.NotNullOrEmpty(item.VolumeId, nameof(item), nameof(item.VolumeId));
        Ensure.NotNullOrEmpty(item.Name, nameof(item), nameof(item.Name));

        var nameToLog = _logger.GetSensitiveValueForLogging(item.Name);
        _logger.LogInformation("Requested to add shared with me {Type} \"{Name}\"", GetItemTypeName(item), nameToLog);

        using var mappings = await _mappingRegistry.GetMappingsAsync(cancellationToken).ConfigureAwait(false);

        var activeMappings = mappings.GetActive();

        if (activeMappings.Any(m => IsMappingOfSharedWithMeItem(m, item)))
        {
            _logger.LogWarning("Ignored shared with me {Type} \"{Name}\", since it is already mapped", GetItemTypeName(item), nameToLog);
        }

        var sharedWithMeRootPath = _syncFolderPathProvider.GetSharedWithMeRootFolderPath();

        if (string.IsNullOrEmpty(sharedWithMeRootPath))
        {
            _logger.LogWarning("Cannot obtain shared with me root folder path, possibly due to account root folder not being defined");

            return;
        }

        if (!activeMappings.Any(IsSharedWithMeRootFolderMapping))
        {
            mappings.Add(CreateSharedWithMeRootFolderMapping(sharedWithMeRootPath));
        }

        var namesInUse = activeMappings
            .Where(IsSharedWithMeItemMapping)
            .Select(GetLocalName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var uniqueName = GetUniqueName(item.Name, namesInUse, sharedWithMeRootPath, item.IsFolder);

        mappings.Add(CreateSharedWithMeItemMapping(item, uniqueName, sharedWithMeRootPath));

        Save(mappings);
    }

    public async Task RemoveSharedWithMeItemAsync(SharedWithMeItem item, CancellationToken cancellationToken)
    {
        using var mappings = await _mappingRegistry.GetMappingsAsync(cancellationToken).ConfigureAwait(false);

        var nameToLog = _logger.GetSensitiveValueForLogging(item.Name);
        _logger.LogInformation(
            "Requested to remove shared with me {Type} \"{Name}\" (remote volume ID={VolumeId}, link ID={LinkId}, share ID={ShareId})",
            GetItemTypeName(item),
            nameToLog,
            item.VolumeId,
            item.LinkId,
            item.Id);

        var mapping = mappings.GetActive().FirstOrDefault(m => IsMappingOfSharedWithMeItem(m, item));

        if (mapping is null)
        {
            _logger.LogWarning("Unable to find mapping for shared with me sync {Type} \"{Name}\"", GetItemTypeName(item), nameToLog);

            return;
        }

        mappings.Delete(mapping);

        TryDeleteSharedWithMeRootFolderMapping(mappings);

        Save(mappings);
    }

    internal Task WaitForCompletionAsync()
    {
        return _mappingMaintenance.WaitForCompletionAsync();
    }

    private static RemoteToLocalMapping CreateSharedWithMeRootFolderMapping(string folderPath)
    {
        return new RemoteToLocalMapping
        {
            Type = MappingType.SharedWithMeRootFolder,
            SyncMethod = SyncMethod.OnDemand,
            Local =
            {
                Path = folderPath,
            },
        };
    }

    private static RemoteToLocalMapping CreateSharedWithMeItemMapping(SharedWithMeItem item, string localItemName, string sharedWithMeRootPath)
    {
        return new RemoteToLocalMapping
        {
            Type = MappingType.SharedWithMeItem,
            SyncMethod = SyncMethod.OnDemand,
            Local =
            {
                Path = Path.Combine(sharedWithMeRootPath, localItemName),
            },
            Remote =
            {
                ShareId = item.Id,
                RootLinkId = item.LinkId,
                VolumeId = item.VolumeId,
                RootItemName = item.Name,
                IsReadOnly = item.IsReadOnly,
                RootItemType = item.IsFolder ? LinkType.Folder : LinkType.File,
            },
        };
    }

    private static RemoteToLocalMapping CreateSharedWithMeItemMapping(RemoteToLocalMapping mapping, string localItemName, string sharedWithMeRootPath)
    {
        return new RemoteToLocalMapping
        {
            Type = mapping.Type,
            SyncMethod = SyncMethod.OnDemand,
            Local =
            {
                Path = Path.Combine(sharedWithMeRootPath, localItemName),
            },
            Remote =
            {
                RootLinkId = mapping.Remote.RootLinkId,
                ShareId = mapping.Remote.ShareId,
                VolumeId = mapping.Remote.VolumeId,
                RootItemName = mapping.Remote.RootItemName,
                IsReadOnly = mapping.Remote.IsReadOnly,
                RootItemType = mapping.Remote.RootItemType,
            },
        };
    }

    private static bool IsMappingOfSharedWithMeItem(RemoteToLocalMapping mapping, SharedWithMeItem item)
    {
        return IsSharedWithMeItemMapping(mapping)
            && ((mapping.Remote.RootLinkId == item.LinkId
                    && mapping.Remote.VolumeId == item.VolumeId)
                || (mapping.Remote.ShareId == item.Id));
    }

    private static bool IsSharedWithMeMapping(RemoteToLocalMapping mapping) => IsSharedWithMeRootFolderMapping(mapping) || IsSharedWithMeItemMapping(mapping);
    private static bool IsSharedWithMeRootFolderMapping(RemoteToLocalMapping mapping) => mapping.Type is MappingType.SharedWithMeRootFolder;
    private static bool IsSharedWithMeItemMapping(RemoteToLocalMapping mapping) => mapping.Type is MappingType.SharedWithMeItem;

    private static string GetLocalName(RemoteToLocalMapping mapping)
    {
        return Path.GetFileName(mapping.Local.Path);
    }

    private static string GetItemTypeName(SharedWithMeItem item) => item.IsFolder ? "folder" : "file";

    private string GetUniqueName(string name, HashSet<string> namesInUse, string parentPath, bool isFolder)
    {
        var uniqueName = _numberSuffixedNameGenerator
            .GenerateNames(name, isFolder ? NameType.Folder : NameType.File)
            .First(
                candidateName =>
                {
                    var itemPath = Path.Combine(parentPath, candidateName);
                    return !namesInUse.Contains(candidateName)
                        && !_localFolderService.FolderExists(itemPath)
                        && !_localFolderService.FileExists(itemPath);
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
        var sharedWithMeRootFolderPath = _syncFolderPathProvider.GetSharedWithMeRootFolderPath();

        if (cloudFilesMapping == null || string.IsNullOrEmpty(sharedWithMeRootFolderPath))
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
                return CreateSharedWithMeRootFolderMapping(sharedWithMeRootFolderPath);
            }

            var uniqueName = GetUniqueName(
                mapping.Remote.RootItemName ?? string.Empty,
                namesInUse,
                sharedWithMeRootFolderPath,
                mapping.Remote.RootItemType is LinkType.Folder);

            return CreateSharedWithMeItemMapping(mapping, uniqueName, sharedWithMeRootFolderPath);
        }
    }

    private void TryDeleteSharedWithMeRootFolderMapping(IUpdatableMappings mappings)
    {
        var activeMappings = mappings.GetActive();

        if (activeMappings.Any(IsSharedWithMeItemMapping))
        {
            return;
        }

        var itemsMapping = activeMappings.FirstOrDefault(IsSharedWithMeRootFolderMapping);

        if (itemsMapping == null)
        {
            _logger.LogWarning("Unable to find mapping for shared with me root folder");

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
