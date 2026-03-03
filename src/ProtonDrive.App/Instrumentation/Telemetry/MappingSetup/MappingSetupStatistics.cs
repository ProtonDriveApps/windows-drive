using System.Collections.Concurrent;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Mapping.SyncFolders;
using ProtonDrive.App.Photos.Import;
using ProtonDrive.App.Settings;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.App.Instrumentation.Telemetry.MappingSetup;

internal sealed class MappingSetupStatistics : IMappingsAware, IMappingStateAware, IPhotoImportFoldersAware
{
    private readonly ConcurrentDictionary<int, MappingSetupDetails> _mappingStatisticsById = new();

    void IMappingsAware.OnMappingsChanged(IReadOnlyCollection<RemoteToLocalMapping> activeMappings, IReadOnlyCollection<RemoteToLocalMapping> deletedMappings)
    {
        var processedMappingIds = new HashSet<int>();

        foreach (var mapping in activeMappings.Concat(deletedMappings).Where(x => x.Type is not MappingType.SharedWithMeRootFolder))
        {
            processedMappingIds.Add(mapping.Id);

            var mappingSetupDetails = new MappingSetupDetails(
                mapping.Type,
                mapping.Remote.RootItemType,
                mapping.SyncMethod,
                mapping.Status,
                MappingSetupStatus.None,
                mapping.Remote.IsReadOnly);

            _mappingStatisticsById.TryAdd(
                mapping.Id,
                mappingSetupDetails);
        }

        foreach (var unprocessedMappingId in _mappingStatisticsById.Keys.Where(id => !processedMappingIds.Contains(id)).ToList())
        {
            _mappingStatisticsById.TryRemove(unprocessedMappingId, out _);
        }
    }

    void IMappingStateAware.OnMappingStateChanged(RemoteToLocalMapping mapping, MappingState mappingSetup)
    {
        if (mapping.Type is MappingType.SharedWithMeRootFolder ||
            mappingSetup.Status is not (MappingSetupStatus.Failed or MappingSetupStatus.Succeeded))
        {
            return;
        }

        if (!_mappingStatisticsById.TryGetValue(mapping.Id, out var existingItem))
        {
            return;
        }

        var mappingSetupDetails = new MappingSetupDetails(
            mapping.Type,
            mapping.Remote.RootItemType,
            mapping.SyncMethod,
            mapping.Status,
            mappingSetup.Status,
            mapping.Remote.IsReadOnly);

        _mappingStatisticsById.TryUpdate(
            mapping.Id,
            mappingSetupDetails,
            existingItem);
    }

    void IPhotoImportFoldersAware.OnPhotoImportFolderChanged(SyncFolderChangeType changeType, PhotoImportFolderState folder)
    {
        if (changeType is not SyncFolderChangeType.Updated)
        {
            return;
        }

        if (!_mappingStatisticsById.TryGetValue(folder.MappingId, out var existingItem))
        {
            return;
        }

        var setupStatus = folder.Status switch
        {
            PhotoImportFolderStatus.Succeeded => MappingSetupStatus.Succeeded,
            PhotoImportFolderStatus.Importing or PhotoImportFolderStatus.Interrupted => MappingSetupStatus.PartiallySucceeded,
            PhotoImportFolderStatus.Failed or PhotoImportFolderStatus.SetupFailed or PhotoImportFolderStatus.ValidationFailed => MappingSetupStatus.Failed,
            _ => existingItem.SetupStatus,
        };

        var mappingSetupDetails = new MappingSetupDetails(
            existingItem.Type,
            existingItem.LinkType,
            existingItem.SyncMethod,
            existingItem.Status,
            setupStatus,
            isReadOnly: true);

        _mappingStatisticsById.TryUpdate(
            folder.MappingId,
            mappingSetupDetails,
            existingItem);
    }

    public IReadOnlyCollection<MappingSetupDetails> GetMappingDetails()
    {
        return _mappingStatisticsById.Values.AsReadOnlyCollection(_mappingStatisticsById.Values.Count);
    }
}
