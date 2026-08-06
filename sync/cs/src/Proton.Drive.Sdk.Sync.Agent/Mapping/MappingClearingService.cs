using MoreLinq.Extensions;
using Proton.Drive.Sdk.Sync.Agent.Account;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.OnDemand;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping;

internal sealed class MappingClearingService : IMappingsAware, IAccountSwitchingHandler
{
    private readonly IMappingTeardownPipeline _mappingTeardown;
    private readonly IMappingRegistry _mappingRegistry;
    private readonly IOnDemandSyncRootRegistry _onDemandSyncRootRegistry;
    private readonly IShellSyncFolderRegistry _shellSyncFolderRegistry;

    private IReadOnlyCollection<RemoteToLocalMapping> _deletedMappings = [];

    public MappingClearingService(
        IMappingTeardownPipeline mappingTeardown,
        IMappingRegistry mappingRegistry,
        IOnDemandSyncRootRegistry onDemandSyncRootRegistry,
        IShellSyncFolderRegistry shellSyncFolderRegistry)
    {
        _mappingTeardown = mappingTeardown;
        _mappingRegistry = mappingRegistry;
        _onDemandSyncRootRegistry = onDemandSyncRootRegistry;
        _shellSyncFolderRegistry = shellSyncFolderRegistry;
    }

    void IMappingsAware.OnMappingsChanged(
        IReadOnlyCollection<RemoteToLocalMapping> activeMappings,
        IReadOnlyCollection<RemoteToLocalMapping> deletedMappings)
    {
        _deletedMappings = deletedMappings;
    }

    async Task<bool> IAccountSwitchingHandler.HandleAccountSwitchingAsync(CancellationToken cancellationToken)
    {
        await DeleteActiveMappingsAsync(cancellationToken).ConfigureAwait(false);
        await TearDownLocalFoldersAsync(cancellationToken).ConfigureAwait(false);
        await ClearMappingsAsync(cancellationToken).ConfigureAwait(false);
        ClearRegistries();

        // We do not care whether tearing down local folders succeeded or failed.
        // If it failed, we silently ignore the failure and continue user account switching.
        return true;
    }

    private async Task DeleteActiveMappingsAsync(CancellationToken cancellationToken)
    {
        using var mappings = await _mappingRegistry.GetMappingsAsync(cancellationToken).ConfigureAwait(false);

        mappings.GetActive().OrderDescending(HierarchicalMappingComparer.Instance).ForEach(mappings.Delete);
    }

    private async Task TearDownLocalFoldersAsync(CancellationToken cancellationToken)
    {
        var mappingsToTearDown = _deletedMappings
            .Where(x => x.Status is not MappingStatus.TornDown)
            .OrderDescending(HierarchicalMappingComparer.Instance);

        foreach (var mapping in mappingsToTearDown)
        {
            // Remote folder cannot be torn down, because after user account switching,
            // current user account is not the owner of remote folder.
            mapping.Remote = new RemoteReplica
            {
                RootItemType = mapping.Remote.RootItemType,
                IsReadOnly = mapping.Remote.IsReadOnly,
            };

            await _mappingTeardown.TearDownAsync(mapping, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task ClearMappingsAsync(CancellationToken cancellationToken)
    {
        return _mappingRegistry.ClearAsync(cancellationToken);
    }

    private void ClearRegistries()
    {
        _onDemandSyncRootRegistry.TryUnregisterAll();
        _shellSyncFolderRegistry.Unregister();
    }
}
