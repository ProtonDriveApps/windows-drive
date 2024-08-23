using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.Sync.Agent.Validation;

public sealed class StateConsistencyGuard<TId>
    where TId : IEquatable<TId>
{
    private readonly SyncEngineStateConsistencyGuard<TId> _syncEngineStateConsistencyGuard;

    public StateConsistencyGuard(
        IAltIdentifiableTreeNodeRepository<SyncedTreeNodeModel<TId>, TId, TId> syncedTreeRepository,
        ITreeNodeRepository<UpdateTreeNodeModel<TId>, TId> localUpdateTreeRepository,
        ITreeNodeRepository<UpdateTreeNodeModel<TId>, TId> remoteUpdateTreeRepository,
        ITransactionProvider transactionProvider,
        ILoggerFactory loggerFactory)
    {
        var syncedTree = new SyncedTree<TId>(
            syncedTreeRepository,
            new SyncedTreeNodeFactory<TId>());

        var remoteUpdateTree = new UpdateTree<TId>(
            remoteUpdateTreeRepository,
            new UpdateTreeNodeFactory<TId>());

        var localUpdateTree = new UpdateTree<TId>(
            localUpdateTreeRepository,
            new UpdateTreeNodeFactory<TId>());

        var logger = loggerFactory.CreateLogger<SyncEngineStateConsistencyGuard<TId>>();
        var scheduler = new TransactedScheduler(logger, new SerialScheduler(), transactionProvider);

        _syncEngineStateConsistencyGuard = new SyncEngineStateConsistencyGuard<TId>(
            syncedTree,
            localUpdateTree,
            remoteUpdateTree,
            scheduler,
            logger);
    }

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        return _syncEngineStateConsistencyGuard.VerifyAndFixStateAsync(cancellationToken);
    }
}
