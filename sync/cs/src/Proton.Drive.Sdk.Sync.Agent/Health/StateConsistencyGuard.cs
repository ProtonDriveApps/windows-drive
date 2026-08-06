using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Synced;
using Proton.Drive.Sdk.Sync.Engine.Shared.Trees.Update;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Trees;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Agent.Health;

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
