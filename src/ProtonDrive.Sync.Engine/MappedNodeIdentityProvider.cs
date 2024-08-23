using System;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Adapters;

namespace ProtonDrive.Sync.Engine;
internal sealed class MappedNodeIdentityProvider<TId> : IMappedNodeIdentityProvider<TId>
    where TId : struct, IEquatable<TId>
{
    private readonly Replica _replica;
    private readonly SyncedTree<TId> _syncedTree;
    private readonly IScheduler _syncScheduler;

    public MappedNodeIdentityProvider(Replica replica, SyncedTree<TId> syncedTree, IScheduler syncScheduler)
    {
        _replica = replica;
        _syncedTree = syncedTree;
        _syncScheduler = syncScheduler;
    }

    public Task<TId?> GetMappedNodeIdOrDefaultAsync(TId id, CancellationToken cancellationToken)
    {
        return Schedule(() => GetMappedNodeIdOrDefault(id), cancellationToken);
    }

    private TId? GetMappedNodeIdOrDefault(TId id)
    {
        return _syncedTree.NodeByOwnIdOrDefault(id, _replica)?.Model.OtherId(_replica);
    }

    private Task<T> Schedule<T>(Func<T> origin, CancellationToken cancellationToken)
    {
        return _syncScheduler.Schedule(origin, cancellationToken);
    }
}
