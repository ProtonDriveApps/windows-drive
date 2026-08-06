using System.Collections.Concurrent;
using Proton.Drive.Sdk.Sync.Agent.Mapping;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry.MappingSetup;

internal sealed class MappingSetupStatistics : IMappingsAware, IMappingStateAware
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

    public IReadOnlyCollection<MappingSetupDetails> GetMappingDetails()
    {
        return _mappingStatisticsById.Values.AsReadOnlyCollection(_mappingStatisticsById.Values.Count);
    }
}
