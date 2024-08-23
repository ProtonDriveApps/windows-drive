using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoreLinq.Extensions;
using ProtonDrive.App.Account;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;

namespace ProtonDrive.App.Mapping;

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

        mappings.GetActive().ForEach(mappings.Delete);
    }

    private async Task TearDownLocalFoldersAsync(CancellationToken cancellationToken)
    {
        foreach (var mapping in _deletedMappings.Where(x => x.Status is not MappingStatus.TornDown))
        {
            // Remote folder cannot be torn down, because current user account
            // is not owner of the remote folder.
            mapping.Remote = new RemoteReplica();

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
