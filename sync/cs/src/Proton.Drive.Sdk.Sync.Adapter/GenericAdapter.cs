using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Adapter.NodeCopying;
using Proton.Drive.Sdk.Sync.Adapter.OnDemandHydration;
using Proton.Drive.Sdk.Sync.Adapter.OnDemandHydration.FileSizeCorrection;
using Proton.Drive.Sdk.Sync.Adapter.OperationExecution;
using Proton.Drive.Sdk.Sync.Adapter.Shared;
using Proton.Drive.Sdk.Sync.Adapter.SyncStateMaintenance;
using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter.NodeLinking;
using Proton.Drive.Sdk.Sync.Adapter.Trees.Dirty;
using Proton.Drive.Sdk.Sync.Adapter.Trees.StateMaintenance;
using Proton.Drive.Sdk.Sync.Adapter.UpdateDetection;
using Proton.Drive.Sdk.Sync.Adapter.UpdateDetection.LogBased;
using Proton.Drive.Sdk.Sync.Adapter.UpdateDetection.StateBased;
using Proton.Drive.Sdk.Sync.Adapter.UpdateDetection.StateBased.Enumeration;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Adapters;
using Proton.Drive.Sdk.Sync.Shared.Diagnostics.TemporaryFiles;
using Proton.Drive.Sdk.Sync.Shared.ExecutionStatistics;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Property;
using Proton.Drive.Sdk.Sync.Shared.SyncActivity;
using Proton.Drive.Sdk.Sync.Shared.Trees;
using Proton.Drive.Sdk.Sync.Shared.Trees.Changes;
using Proton.Drive.Sdk.Sync.Shared.Trees.Operations;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Features;
using Proton.Drive.Shared.Reporting;
using Proton.Drive.Shared.Repository;
using Proton.Drive.Shared.Telemetry;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Adapter;

public sealed class GenericAdapter<TId, TAltId> : ISyncAdapter<TId>, IManagedAdapter<TId>
    where TId : struct, IEquatable<TId>, IComparable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILooseCompoundAltIdentifiableTreeNodeRepository<AdapterTreeNodeModel<TId, TAltId>, TId, TAltId> _adapterTreeRepository;
    private readonly ITreeChangeRepository<TId> _detectedUpdateRepository;
    private readonly IIdentitySource<TId> _idSource;
    private readonly IFileSystemClient<TAltId> _fileSystemClient;
    private readonly IErrorReporting _errorReporting;

    private readonly ExternalFileRevisionProviderProxy _externalFileRevisionProvider;
    private readonly MappedNodeIdentityProviderProxy _mappedNodeIdProvider;
    private readonly DirtyNodes<TId, TAltId> _dirtyNodes;
    private readonly SyncActivity<TId> _syncActivity;
    private readonly IOperationExecutor<TId> _operationExecution;
    private readonly IUpdateDetector _updateDetection;
    private readonly DetectedTreeChanges<TId> _detectedUpdates;
    private readonly ReceivedTreeChanges<TId>? _syncedUpdates;
    private readonly SyncedStateHandler<TId, TAltId>? _syncedStateHandler;
    private readonly FileSyncStateHandler<TId, TAltId>? _fileSyncStateHandler;
    private readonly IFileRevisionProvider<TId> _fileRevisionProvider;
    private readonly HydrationDemandHandler<TId, TAltId> _fileHydrationDemandHandler;
    private readonly PseudoUpdateTrigger<TId, TAltId> _manualUpdateTrigger;

    private readonly ConcurrentExecutionStatistics _stateBasedUpdateDetectionExecutionStatistics = new();
    private readonly FileSystemAccessRateLimiter<TId> _accessRateLimiter;

    private bool _isDisconnected = true;

    public GenericAdapter(
        ILoggerFactory loggerFactory,
        Replica replica,
        AppConfig appConfig,
        IScheduler scheduler,
        ILooseCompoundAltIdentifiableTreeNodeRepository<AdapterTreeNodeModel<TId, TAltId>, TId, TAltId> adapterTreeRepository,
        INodeLinkRepository<TId> nodeLinkRepository,
        ITreeNodeRepository<DirtyTreeNodeModel<TId>, TId> dirtyTreeRepository,
        ITreeNodeRepository<StateMaintenanceTreeNodeModel<TId>, TId>? stateMaintenanceTreeRepository,
        ITreeChangeRepository<TId> detectedUpdateRepository,
        IPropertyRepository propertyRepository,
        ITransactionProvider transactionProvider,
        IIdentitySource<TId> idSource,
        IIdentitySource<TId> detectedUpdateIdSource,
        IIdentitySource<long> contentVersionSource,
        IFileNameFactory<TId> tempFileNameFactory,
        IReadOnlyCollection<string> specialFolderNames,
        TimeSpan maxFileAccessRetryInterval,
        TimeSpan maxFileRevisionCreationInterval,
        TimeSpan minDelayBeforeFileUpload,
        TimeSpan syncDelayInterval,
        IFileSystemClient<TAltId> fileSystemClient,
        IEventLogClient<TAltId> eventLogClient,
        IClock clock,
        IErrorCounter errorCounter,
        IExcelTemporaryFileDetectionCounter excelTemporaryFileDetectionCounter,
        IFeatureFlagProvider featureFlagProvider,
        IErrorReporting errorReporting)
    {
        _adapterTreeRepository = adapterTreeRepository;
        _detectedUpdateRepository = detectedUpdateRepository;
        _idSource = idSource;
        _fileSystemClient = fileSystemClient;
        _errorReporting = errorReporting;

        var syncRoots = new Dictionary<TId, RootInfo<TAltId>>();

        var executionScheduler = new SerialScheduler();
        SyncScheduler = new IntermittentTransactedScheduler(
            loggerFactory.CreateLogger<GenericAdapter<TId, TAltId>>(),
            new SerialScheduler(),
            transactionProvider,
            clock,
            ex => ex is EscapeException or FileRevisionProviderException or HydrationException);

        _externalFileRevisionProvider = new ExternalFileRevisionProviderProxy();
        _mappedNodeIdProvider = new MappedNodeIdentityProviderProxy();

        FileSystemTree = new AdapterTree<TId, TAltId>(adapterTreeRepository, new AdapterTreeNodeFactory<TId, TAltId>());
        DirtyTree = new DirtyTree<TId>(dirtyTreeRepository, new DirtyTreeNodeFactory<TId>());

        var copiedNodes = new CopiedNodesHandler<TId, TAltId>(
            loggerFactory.CreateLogger<CopiedNodesHandler<TId, TAltId>>(),
            FileSystemTree,
            DirtyTree,
            nodeLinkRepository);

        _dirtyNodes = new DirtyNodes<TId, TAltId>(FileSystemTree, DirtyTree);

        var detectedUpdateIdentitySource =
            new PersistentIdentitySource<TId>(
                detectedUpdateIdSource,
                new TransactedCachingRepository<TId>(
                    transactionProvider,
                    new NamedProperty<TId>("LastDetectedUpdateId", propertyRepository)));

        _detectedUpdates = new DetectedTreeChanges<TId>(
            detectedUpdateIdentitySource,
            detectedUpdateRepository,
            transactionProvider,
            SyncScheduler);

        DetectedUpdates = _detectedUpdates;

        _syncActivity = new SyncActivity<TId>();

        var updateDetectionSequencer = new UpdateDetectionSequencer();
        var fileVersionMapping = new FileVersionMapping<TId, TAltId>();
        _accessRateLimiter = new FileSystemAccessRateLimiter<TId>(clock, maxFileAccessRetryInterval, maxFileRevisionCreationInterval);

        var preconditionsValidationStep = new OperationExecution.PreconditionsValidationStep<TId, TAltId>(
            loggerFactory.CreateLogger<OperationExecution.PreconditionsValidationStep<TId, TAltId>>(),
            FileSystemTree,
            DirtyTree,
            syncRoots,
            _detectedUpdates);

        var contentVersionSequence =
            new PersistentIdentitySource<long>(
                contentVersionSource,
                new TransactedCachingRepository<long>(
                    transactionProvider,
                    new NamedProperty<long>("LastFileContentVersion", propertyRepository)));

        var nodeUpdateDetection = new NodeUpdateDetection<TId, TAltId>(
            loggerFactory.CreateLogger<NodeUpdateDetection<TId, TAltId>>(),
            idSource,
            FileSystemTree,
            _detectedUpdates,
            contentVersionSequence,
            new UpdateLogging<TId, TAltId>(
                loggerFactory.CreateLogger<NodeUpdateDetection<TId, TAltId>>(),
                replica,
                FileSystemTree),
            excelTemporaryFileDetectionCounter);

        var exclusionFilter = new ItemExclusionFilter(specialFolderNames);

        var operationExecutionPreparationStep = new PreparationStep<TId, TAltId>(
            FileSystemTree,
            syncRoots);

        var operationExecutionStep = new NotifyingExecutionStep<TId, TAltId>(
            new ExecutionStep<TId, TAltId>(fileSystemClient, tempFileNameFactory),
            _syncActivity,
            _externalFileRevisionProvider,
            errorCounter);

        var operationExecutionSuccessStep = new OperationExecution.SuccessStep<TId, TAltId>(
            loggerFactory.CreateLogger<OperationExecution.SuccessStep<TId, TAltId>>(),
            replica,
            FileSystemTree,
            _dirtyNodes,
            idSource,
            syncRoots,
            copiedNodes,
            nodeUpdateDetection,
            exclusionFilter,
            fileVersionMapping);

        _operationExecution =
            new OperationExecutionPipeline<TId, TAltId>(
                loggerFactory.CreateLogger<OperationExecutionPipeline<TId, TAltId>>(),
                executionScheduler,
                SyncScheduler,
                copiedNodes,
                updateDetectionSequencer,
                _accessRateLimiter,
                preconditionsValidationStep,
                operationExecutionPreparationStep,
                operationExecutionStep,
                operationExecutionSuccessStep,
                new FailureStep<TId, TAltId>(
                    loggerFactory.CreateLogger<FailureStep<TId, TAltId>>(),
                    FileSystemTree),
                new NameConflictStep<TId, TAltId>(
                    loggerFactory.CreateLogger<NameConflictStep<TId, TAltId>>(),
                    FileSystemTree,
                    preconditionsValidationStep),
                new LoggingStep<TId, TAltId>(
                    loggerFactory.CreateLogger<OperationExecutionPipeline<TId, TAltId>>()));

        var fileSystemEnumeration = new FileSystemEnumeration<TId, TAltId>(
            fileSystemClient,
            syncRoots);

        _manualUpdateTrigger = new PseudoUpdateTrigger<TId, TAltId>(
            SyncScheduler,
            FileSystemTree,
            syncRoots,
            contentVersionSequence,
            nodeUpdateDetection,
            loggerFactory.CreateLogger<PseudoUpdateTrigger<TId, TAltId>>());

        var rootEnumeration = new RootEnumeration<TId, TAltId>(
            SyncScheduler,
            FileSystemTree,
            fileSystemEnumeration,
            new RootEnumerationSuccessStep<TId, TAltId>(
                replica,
                FileSystemTree,
                _dirtyNodes,
                idSource,
                syncRoots,
                copiedNodes,
                nodeUpdateDetection,
                exclusionFilter,
                new RootMigrationStep<TId, TAltId>(
                    loggerFactory.CreateLogger<RootMigrationStep<TId, TAltId>>(),
                    FileSystemTree),
                loggerFactory.CreateLogger<RootEnumerationSuccessStep<TId, TAltId>>()),
            new RootEnumerationCompletionStep<TId, TAltId>(
                nodeUpdateDetection));

        var dirtyNodeUpdateDetection = new DirtyNodeUpdateDetection<TId, TAltId>(
            new NodeEnumeration<TId, TAltId>(
                SyncScheduler,
                fileSystemEnumeration,
                new NodeEnumerationSuccessStep<TId, TAltId>(
                    loggerFactory.CreateLogger<NodeEnumerationSuccessStep<TId, TAltId>>(),
                    replica,
                    FileSystemTree,
                    _dirtyNodes,
                    idSource,
                    syncRoots,
                    copiedNodes,
                    nodeUpdateDetection,
                    exclusionFilter),
                new EnumerationFailureStep<TId, TAltId>(
                    loggerFactory.CreateLogger<EnumerationFailureStep<TId, TAltId>>(),
                    FileSystemTree),
                loggerFactory.CreateLogger<NodeEnumeration<TId, TAltId>>()),
            new ChildrenEnumeration<TId, TAltId>(
                SyncScheduler,
                fileSystemEnumeration,
                new ChildrenEnumerationPreparationStep<TId, TAltId>(
                    nodeUpdateDetection),
                new ChildrenEnumerationSuccessStep<TId, TAltId>(
                    loggerFactory.CreateLogger<ChildrenEnumerationSuccessStep<TId, TAltId>>(),
                    replica,
                    FileSystemTree,
                    _dirtyNodes,
                    idSource,
                    syncRoots,
                    copiedNodes,
                    nodeUpdateDetection,
                    exclusionFilter),
                new EnumerationFailureStep<TId, TAltId>(
                    loggerFactory.CreateLogger<EnumerationFailureStep<TId, TAltId>>(),
                    FileSystemTree),
                new ChildrenEnumerationCompletionStep<TId, TAltId>(
                    nodeUpdateDetection),
                loggerFactory.CreateLogger<ChildrenEnumeration<TId, TAltId>>()),
            _syncActivity,
            _stateBasedUpdateDetectionExecutionStatistics);

        var deletionDetection = new DeletionDetection<TId, TAltId>(
            loggerFactory.CreateLogger<DeletionDetection<TId, TAltId>>(),
            replica,
            SyncScheduler,
            FileSystemTree,
            DirtyTree,
            syncRoots,
            nodeUpdateDetection,
            errorReporting);

        var stateBasedUpdateDetection = new StateBasedUpdateDetection<TId, TAltId>(
            loggerFactory.CreateLogger<StateBasedUpdateDetection<TId, TAltId>>(),
            replica,
            executionScheduler,
            SyncScheduler,
            FileSystemTree,
            DirtyTree,
            syncRoots,
            rootEnumeration,
            dirtyNodeUpdateDetection,
            deletionDetection,
            _stateBasedUpdateDetectionExecutionStatistics);

        var twoPassUpdateDetectionSwitch = new TwoPassUpdateDetectionSwitch(
            featureFlagProvider,
            replica,
            loggerFactory.CreateLogger<TwoPassUpdateDetectionSwitch>());

        var logBasedUpdateDetection = new LogBasedUpdateDetection<TId, TAltId>(
            loggerFactory,
            replica,
            executionScheduler,
            SyncScheduler,
            FileSystemTree,
            _dirtyNodes,
            eventLogClient,
            syncRoots,
            idSource,
            nodeUpdateDetection,
            fileVersionMapping,
            copiedNodes,
            exclusionFilter,
            updateDetectionSequencer,
            twoPassUpdateDetectionSwitch);

        var updateDetection = new UpdateDetection<TId, TAltId>(
            stateBasedUpdateDetection,
            logBasedUpdateDetection,
            twoPassUpdateDetectionSwitch);

        _updateDetection = replica switch
        {
            Replica.Local => new BulkDeletionDetectingUpdateDetectorDecorator(
                updateDetection,
                new BulkDeletionDetector(),
                syncDelayInterval,
                featureFlagProvider,
                clock,
                loggerFactory.CreateLogger<BulkDeletionDetectingUpdateDetectorDecorator>()),

            Replica.Remote => updateDetection,

            _ => throw new ArgumentOutOfRangeException(nameof(replica), replica, null),
        };

        if (stateMaintenanceTreeRepository != null)
        {
            var lastSyncedUpdateProperty = new TransactedCachingRepository<TId>(
                transactionProvider,
                new NamedProperty<TId>("LastSyncedUpdateId", propertyRepository));

            _syncedUpdates = new ReceivedTreeChanges<TId>(
                lastSyncedUpdateProperty,
                transactionProvider);

            _syncedStateHandler = new SyncedStateHandler<TId, TAltId>(
                loggerFactory.CreateLogger<SyncedStateHandler<TId, TAltId>>(),
                SyncScheduler,
                FileSystemTree,
                _syncedUpdates);

            StateMaintenanceTree = new StateMaintenanceTree<TId>(stateMaintenanceTreeRepository, new StateMaintenanceTreeNodeFactory<TId>());
            _ = new StateMaintenanceTreeHandler<TId, TAltId>(FileSystemTree, StateMaintenanceTree);

            _fileSyncStateHandler = new FileSyncStateHandler<TId, TAltId>(
                loggerFactory.CreateLogger<FileSyncStateHandler<TId, TAltId>>(),
                replica,
                appConfig,
                scheduler,
                executionScheduler,
                SyncScheduler,
                FileSystemTree,
                StateMaintenanceTree,
                syncRoots,
                fileSystemClient,
                _accessRateLimiter,
                new FailureStep<TId, TAltId>(
                    loggerFactory.CreateLogger<FailureStep<TId, TAltId>>(),
                    FileSystemTree));
        }

        var copySourceRevisionProvider = new CopySourceRevisionProvider<TId, TAltId>(
            loggerFactory.CreateLogger<CopySourceRevisionProvider<TId, TAltId>>(),
            SyncScheduler,
            copiedNodes,
            _externalFileRevisionProvider,
            _mappedNodeIdProvider);

        _fileRevisionProvider =
            new FallbackFileRevisionProviderDecorator<TId, TAltId>(
                new FileRevisionProvider<TId, TAltId>(
                    SyncScheduler,
                    FileSystemTree,
                    fileSystemClient,
                    syncRoots,
                    new EnumerationFailureStep<TId, TAltId>(
                        loggerFactory.CreateLogger<EnumerationFailureStep<TId, TAltId>>(),
                        FileSystemTree),
                    minDelayBeforeFileUpload,
                    loggerFactory.CreateLogger<FileRevisionProvider<TId, TAltId>>()),
                copySourceRevisionProvider);

        var fileSizeCorrector = new FileSizeCorrectionPipeline<TId, TAltId>(
            loggerFactory.CreateLogger<FileSizeCorrectionPipeline<TId, TAltId>>(),
            SyncScheduler,
            updateDetectionSequencer,
            new OnDemandHydration.FileSizeCorrection.PreconditionsValidationStep<TId, TAltId>(
                loggerFactory.CreateLogger<OnDemandHydration.FileSizeCorrection.PreconditionsValidationStep<TId, TAltId>>(),
                FileSystemTree),
            operationExecutionPreparationStep,
            operationExecutionSuccessStep);

        _fileHydrationDemandHandler = new HydrationDemandHandler<TId, TAltId>(
            loggerFactory.CreateLogger<HydrationDemandHandler<TId, TAltId>>(),
            executionScheduler,
            SyncScheduler,
            FileSystemTree,
            _externalFileRevisionProvider,
            _mappedNodeIdProvider,
            copySourceRevisionProvider,
            fileSizeCorrector,
            _syncActivity);
    }

    public event EventHandler<SyncActivityChangedEventArgs<TId>> SyncActivityChanged
    {
        add => _syncActivity.SyncActivityChanged += value;
        remove => _syncActivity.SyncActivityChanged -= value;
    }

    public bool HasUpdatesToDetect => !_dirtyNodes.IsEmpty;
    public bool HasUpdatesToSynchronize => !_detectedUpdates.IsEmpty;

    public ITreeChangeProvider<TId> DetectedUpdates { get; }
    public IExecutionStatistics ExecutionStatistics => _updateDetection.ExecutionStatistics;

    public ITransactedScheduler SyncScheduler { get; }

    internal AdapterTree<TId, TAltId> FileSystemTree { get; }
    internal DirtyTree<TId> DirtyTree { get; }
    internal StateMaintenanceTree<TId>? StateMaintenanceTree { get; }

    public void Initialize(
        IFileRevisionProvider<TId> fileRevisionProvider,
        IMappedNodeIdentityProvider<TId> mappedNodeIdProvider,
        ITreeChangeProvider<TId> syncedStateProvider)
    {
        _externalFileRevisionProvider.SetOrigin(fileRevisionProvider);
        _mappedNodeIdProvider.SetOrigin(mappedNodeIdProvider);

        _idSource.InitializeFrom(_adapterTreeRepository.GetLastId());
        _idSource.InitializeFrom(_detectedUpdateRepository.GetLastNodeId());

        _dirtyNodes.Initialize();
        _detectedUpdates.Initialize();

        _syncedUpdates?.ConnectTo(syncedStateProvider);
    }

    public Task<ExecutionResult<TId>> ExecuteOperation(
        ExecutableOperation<TId> operation,
        CancellationToken cancellationToken)
    {
        return _operationExecution.ExecuteAsync(operation, cancellationToken);
    }

    public Task<ISourceRevision> OpenFileForReadingAsync(TId id, long version, CancellationToken cancellationToken)
    {
        return _fileRevisionProvider.OpenFileForReadingAsync(id, version, cancellationToken);
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _isDisconnected = false;

        // The specific sync root path will be set by the RootedFileSystemClientDecorator
        _fileSystemClient.Connect(syncRootPath: string.Empty, _fileHydrationDemandHandler);

        await _updateDetection.StartAsync(cancellationToken).ConfigureAwait(false);

        _fileSyncStateHandler?.Start();
    }

    public async Task DisconnectAsync()
    {
        if (_isDisconnected)
        {
            return;
        }

        if (_syncedStateHandler != null)
        {
            await _syncedStateHandler.StopAsync().ConfigureAwait(false);
        }

        if (_fileSyncStateHandler != null)
        {
            await _fileSyncStateHandler.StopAsync().ConfigureAwait(false);
        }

        await _updateDetection.StopAsync().ConfigureAwait(false);

        await _fileSystemClient.DisconnectAsync().ConfigureAwait(false);

        await SaveStateAsync().ConfigureAwait(false);

        _isDisconnected = true;
    }

    public Task DetectUpdatesAsync(CancellationToken cancellationToken)
    {
        return _updateDetection.ExecuteAsync(cancellationToken);
    }

    public Task TriggerPseudoFileEditAsync(TId id, long requestedVersion, CancellationToken cancellationToken)
    {
        return _manualUpdateTrigger.TriggerPseudoFileEditAsync(id, requestedVersion, cancellationToken);
    }

    public void Reset()
    {
        _accessRateLimiter.Reset();
    }

    public void Dispose()
    {
        _fileSyncStateHandler?.Dispose();
    }

    public Task<LooseCompoundAltIdentity<TAltId>?> GetNodeAltIdByIdOrDefaultAsync(TId id, CancellationToken cancellationToken)
    {
        return SyncScheduler.Schedule(() => _adapterTreeRepository.NodeById(id)?.AltId, cancellationToken);
    }

    public Task<TId?> GetNodeIdByAltIdOrDefaultAsync(LooseCompoundAltIdentity<TAltId> altId, CancellationToken cancellationToken)
    {
        return SyncScheduler.Schedule(() => _adapterTreeRepository.NodeByAltId(altId)?.Id, cancellationToken);
    }

    private async Task SaveStateAsync()
    {
        try
        {
            // Persist latest state changes
            await SyncScheduler.ScheduleAndCommit(() => { }).ConfigureAwait(false);
        }
        catch
        {
            // Ignore failure to persist latest changes. The database behind the TransactionalScheduler
            // throws an exception if there was an unhandled exception caught on it previously.
        }
    }

    /// <summary>
    /// The internal proxy class is required to be passed to other constructors on
    /// object construction. The origin interface implementation will be set during
    /// object lifetime outside the constructor.
    /// </summary>
    private sealed class ExternalFileRevisionProviderProxy : IFileRevisionProvider<TId>
    {
        private IFileRevisionProvider<TId>? _origin;

        public void SetOrigin(IFileRevisionProvider<TId> value) => _origin = value;

        public Task<ISourceRevision> OpenFileForReadingAsync(TId id, long version, CancellationToken cancellationToken)
            => _origin?.OpenFileForReadingAsync(id, version, cancellationToken)
               ?? throw new InvalidOperationException($"Origin not set using {nameof(SetOrigin)}");
    }

    /// <summary>
    /// The internal proxy class is required to be passed to other constructors on
    /// object construction. The origin interface implementation will be set during
    /// object lifetime outside the constructor.
    /// </summary>
    private sealed class MappedNodeIdentityProviderProxy : IMappedNodeIdentityProvider<TId>
    {
        private IMappedNodeIdentityProvider<TId>? _origin;

        public void SetOrigin(IMappedNodeIdentityProvider<TId> value) => _origin = value;

        public Task<TId?> GetMappedNodeIdOrDefaultAsync(TId id, CancellationToken cancellationToken) =>
            _origin?.GetMappedNodeIdOrDefaultAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Origin not set using {nameof(SetOrigin)}");
    }
}
