using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Engine.Consolidation;
using ProtonDrive.Sync.Engine.Propagation;
using ProtonDrive.Sync.Engine.Reconciliation;
using ProtonDrive.Sync.Engine.Shared.Trees.Changes;
using ProtonDrive.Sync.Engine.Shared.Trees.Propagation;
using ProtonDrive.Sync.Engine.Shared.Trees.Synced;
using ProtonDrive.Sync.Engine.Shared.Trees.Update;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.ExecutionStatistics;
using ProtonDrive.Sync.Shared.Property;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.Changes;

namespace ProtonDrive.Sync.Engine;

public class SyncEngine<TId> : IInitializable, IExecutionStatisticsProvider
    where TId : struct, IEquatable<TId>, IComparable<TId>
{
    private readonly ISyncAdapter<TId> _remoteAdapter;
    private readonly ISyncAdapter<TId> _localAdapter;
    private readonly IAltIdentifiableTreeNodeRepository<SyncedTreeNodeModel<TId>, TId, TId> _syncedTreeRepository;
    private readonly ITreeNodeRepository<UpdateTreeNodeModel<TId>, TId> _localUpdateTreeRepository;
    private readonly ITreeNodeRepository<UpdateTreeNodeModel<TId>, TId> _remoteUpdateTreeRepository;
    private readonly IAltIdentifiableTreeNodeRepository<PropagationTreeNodeModel<TId>, TId, TId> _propagationTreeRepository;
    private readonly IIdentitySource<TId> _idSource;

    private readonly ILogger<SyncEngine<TId>> _logger;
    private readonly ITransactedScheduler _syncScheduler;
    private readonly ConcurrentExecutionStatistics _executionStatistics = new();

    private readonly DetectedTreeChanges<TId> _localSyncedUpdates;
    private readonly IReceivedTreeChanges<TId> _localDetectedUpdates;
    private readonly IReceivedTreeChanges<TId> _remoteDetectedUpdates;

    private readonly ConsolidationPipeline<TId> _remoteConsolidation;
    private readonly ConsolidationPipeline<TId> _localConsolidation;
    private readonly ReconciliationPipeline<TId> _reconciliation;
    private readonly TreePropagationPipeline<TId> _propagation;

    private readonly IMappedNodeIdentityProvider<TId> _localMappedNodeIdentityProvider;
    private readonly IMappedNodeIdentityProvider<TId> _remoteMappedNodeIdentityProvider;

    private bool _hasChangesToSync;

    public SyncEngine(
        ILoggerFactory loggerFactory,
        ISyncAdapter<TId> remoteAdapter,
        ISyncAdapter<TId> localAdapter,
        IAltIdentifiableTreeNodeRepository<SyncedTreeNodeModel<TId>, TId, TId> syncedTreeRepository,
        ITreeNodeRepository<UpdateTreeNodeModel<TId>, TId> localUpdateTreeRepository,
        ITreeNodeRepository<UpdateTreeNodeModel<TId>, TId> remoteUpdateTreeRepository,
        IAltIdentifiableTreeNodeRepository<PropagationTreeNodeModel<TId>, TId, TId> propagationTreeRepository,
        ITreeChangeRepository<TId> localSyncedUpdateRepository,
        IPropertyRepository propertyRepository,
        ITransactionProvider transactionProvider,
        IIdentitySource<TId> idSource,
        IIdentitySource<TId> syncedUpdateIdSource,
        IFileNameFactory<TId> nameClashConflictNameFactory,
        IFileNameFactory<TId> deleteConflictNameFactory,
        IFileNameFactory<TId> tempUniqueNameFactory,
        IScheduler scheduler)
    {
        _remoteAdapter = remoteAdapter;
        _localAdapter = localAdapter;
        _syncedTreeRepository = syncedTreeRepository;
        _localUpdateTreeRepository = localUpdateTreeRepository;
        _remoteUpdateTreeRepository = remoteUpdateTreeRepository;
        _propagationTreeRepository = propagationTreeRepository;
        _idSource = idSource;

        _logger = loggerFactory.CreateLogger<SyncEngine<TId>>();
        _syncScheduler = new TransactedScheduler(_logger, scheduler, transactionProvider);

        // Trees
        SyncedTree = new SyncedTree<TId>(
            syncedTreeRepository,
            new SyncedTreeNodeFactory<TId>());

        RemoteUpdateTree = new UpdateTree<TId>(
            remoteUpdateTreeRepository,
            new UpdateTreeNodeFactory<TId>());

        LocalUpdateTree = new UpdateTree<TId>(
            localUpdateTreeRepository,
            new UpdateTreeNodeFactory<TId>());

        PropagationTree = new PropagationTree<TId>(
            propagationTreeRepository,
            new PropagationTreeNodeFactory<TId>());

        // Consolidation
        var localLastUpdateProperty = new TransactedCachingRepository<TId>(
            transactionProvider,
            new NamedProperty<TId>("LastLocalUpdateId", propertyRepository));

        var remoteLastUpdateProperty = new TransactedCachingRepository<TId>(
            transactionProvider,
            new NamedProperty<TId>("LastRemoteUpdateId", propertyRepository));

        _localDetectedUpdates = new ReceivedTreeChanges<TId>(
                localLastUpdateProperty,
                transactionProvider)
            .ConnectTo(localAdapter.DetectedUpdates);

        _remoteDetectedUpdates = new ReceivedTreeChanges<TId>(
                remoteLastUpdateProperty,
                transactionProvider)
            .ConnectTo(remoteAdapter.DetectedUpdates);

        _remoteConsolidation = new ConsolidationPipeline<TId>(
            Replica.Remote,
            _remoteDetectedUpdates,
            SyncedTree,
            RemoteUpdateTree,
            _syncScheduler,
            loggerFactory.CreateLogger<ConsolidationPipeline<TId>>());

        _localConsolidation = new ConsolidationPipeline<TId>(
            Replica.Local,
            _localDetectedUpdates,
            SyncedTree,
            LocalUpdateTree,
            _syncScheduler,
            loggerFactory.CreateLogger<ConsolidationPipeline<TId>>());

        // Reconciliation
        _reconciliation = new ReconciliationPipeline<TId>(
            SyncedTree,
            RemoteUpdateTree,
            LocalUpdateTree,
            PropagationTree,
            _syncScheduler,
            nameClashConflictNameFactory,
            deleteConflictNameFactory,
            loggerFactory.CreateLogger<ReconciliationPipeline<TId>>());

        // Propagation
        _propagation = new TreePropagationPipeline<TId>(
            _syncScheduler,
            remoteAdapter,
            localAdapter,
            SyncedTree,
            RemoteUpdateTree,
            LocalUpdateTree,
            PropagationTree,
            tempUniqueNameFactory,
            _executionStatistics,
            loggerFactory);

        // Synced state updates
        var localSyncedUpdateIdentitySource =
            new PersistentIdentitySource<TId>(
                syncedUpdateIdSource,
                new TransactedCachingRepository<TId>(
                    transactionProvider,
                    new NamedProperty<TId>("LastLocalSyncedUpdateId", propertyRepository)));

        _localSyncedUpdates =
            new DetectedTreeChanges<TId>(
                localSyncedUpdateIdentitySource,
                localSyncedUpdateRepository,
                transactionProvider,
                _syncScheduler);

        var decoratedSyncedUpdates = new RemovingDuplicatesDetectedTreeChangesDecorator<TId>(
            _localSyncedUpdates,
            transactionProvider);

        decoratedSyncedUpdates.DetectLocalChangesOf(SyncedTree, LocalUpdateTree);

        // Node mapping
        _localMappedNodeIdentityProvider = new MappedNodeIdentityProvider<TId>(Replica.Local, SyncedTree, _syncScheduler);
        _remoteMappedNodeIdentityProvider = new MappedNodeIdentityProvider<TId>(Replica.Remote, SyncedTree, _syncScheduler);
    }

    public bool HasOldUpdatesToSynchronize => _hasChangesToSync;
    public bool HasNewUpdatesToSynchronize => !_localDetectedUpdates.IsEmpty || !_remoteDetectedUpdates.IsEmpty;
    public IExecutionStatistics ExecutionStatistics => _executionStatistics;

    internal SyncedTree<TId> SyncedTree { get; }
    internal UpdateTree<TId> RemoteUpdateTree { get; }
    internal UpdateTree<TId> LocalUpdateTree { get; }
    internal PropagationTree<TId> PropagationTree { get; }

    public void Initialize()
    {
        _logger.LogInformation("Initializing Sync Engine");

        _idSource.InitializeFrom(_syncedTreeRepository.GetLastId());
        _idSource.InitializeFrom(_syncedTreeRepository.GetLastAltId());
        _idSource.InitializeFrom(_remoteUpdateTreeRepository.GetLastId());
        _idSource.InitializeFrom(_localUpdateTreeRepository.GetLastId());
        _idSource.InitializeFrom(_propagationTreeRepository.GetLastId());
        _idSource.InitializeFrom(_propagationTreeRepository.GetLastAltId());

        _remoteAdapter.Initialize(
            _localAdapter,
            _remoteMappedNodeIdentityProvider,
            new NullTreeChangeProvider<TId>());

        _localAdapter.Initialize(
            _remoteAdapter,
            _localMappedNodeIdentityProvider,
            _localSyncedUpdates);

        _localSyncedUpdates.Initialize();

        _hasChangesToSync = GetHasChangesToSync();
    }

    public async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        _executionStatistics.ClearFailures();

        try
        {
            _logger.LogInformation("Started synchronization");

            await _remoteConsolidation.Execute(cancellationToken).ConfigureAwait(false);

            await _localConsolidation.Execute(cancellationToken).ConfigureAwait(false);

            await _reconciliation.Execute(cancellationToken).ConfigureAwait(false);

            await _propagation.Execute(cancellationToken).ConfigureAwait(false);

            _hasChangesToSync = await Schedule(GetHasChangesToSync, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Finished synchronization");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Cancelled synchronization");
            _hasChangesToSync = true;

            throw;
        }
    }

    public Task<TId?> GetMappedNodeIdOrDefaultAsync(Replica replica, TId id, CancellationToken cancellationToken)
    {
        return replica switch
        {
            Replica.Remote => _remoteMappedNodeIdentityProvider.GetMappedNodeIdOrDefaultAsync(id, cancellationToken),
            Replica.Local => _localMappedNodeIdentityProvider.GetMappedNodeIdOrDefaultAsync(id, cancellationToken),
            _ => throw new InvalidEnumArgumentException(nameof(replica), (int)replica, typeof(Replica)),
        };
    }

    private bool GetHasChangesToSync()
    {
        return !RemoteUpdateTree.Root.IsLeaf || !LocalUpdateTree.Root.IsLeaf;
    }

    private Task<T> Schedule<T>(Func<T> origin, CancellationToken cancellationToken)
    {
        return _syncScheduler.Schedule(origin, cancellationToken);
    }
}
