using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.FileSystem.Local;
using ProtonDrive.App.FileSystem.Remote;
using ProtonDrive.App.Health;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Settings;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Telemetry;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Adapter;
using ProtonDrive.Sync.Agent.Health;
using ProtonDrive.Sync.DataAccess;
using ProtonDrive.Sync.DataAccess.Databases;
using ProtonDrive.Sync.Engine;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.FileSystem.Integration;

namespace ProtonDrive.App.Sync;

internal sealed class SyncAgentFactory
{
    private const string NameClashConflictNamePattern = "{OriginalName} (# Name clash {CurrentDate} {RandomSuffix}C #){.Extension}";
    private const string DeleteConflictNamePattern = "{OriginalName} (# Delete conflict {CurrentDate} {RandomSuffix}C #){.Extension}";
    private const string EditConflictNamePattern = "{OriginalName} (# Edit conflict {CurrentDate} {RandomSuffix}C #){.Extension}";
    private const string TempUniqueNamePattern = "{OriginalName} (# Temporary renamed {CurrentDate} {RandomSuffix}R #){.Extension}";
    private const string DeletedNamePattern = "{OriginalName}{.Extension} (# Deleted {CurrentDate} {RandomSuffix}D #)";
    private const string TempFileNamePattern = ".~{OriginalName}-Temp-{RandomSuffix}.tmp";

    private static readonly MappingType[] SupportedMappingTypes =
        [
            MappingType.CloudFiles,
            MappingType.HostDeviceFolder,
            MappingType.ForeignDevice,
            MappingType.SharedWithMeRootFolder,
            MappingType.SharedWithMeItem,
        ];

    private readonly AppConfig _appConfig;
    private readonly RemoteDecoratedFileSystemClientFactory _remoteFileSystemClientFactory;
    private readonly RemoteDecoratedEventLogClientFactory _remoteEventLogClientFactory;
    private readonly LocalRootMapForDeletionDetectionFactory _localSyncRootMapForDeletionDetectionFactory;
    private readonly ILocalVolumeInfoProvider _localVolumeInfoProvider;
    private readonly ILocalFileSystemClientFactory _localUndecoratedFileSystemClientFactory;
    private readonly ILocalEventLogClientFactory _localUndecoratedEventLogClientFactory;
    private readonly IRootDeletionHandler _syncRootDeletionHandler;
    private readonly ISyncFolderStructureProtector _folderStructureProtector;
    private readonly IFileConsistencyGuardFactory _fileConsistencyGuardFactory;
    private readonly IScheduler _scheduler;
    private readonly IClock _clock;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IErrorCounter _errorCounter;

    public SyncAgentFactory(
        AppConfig appConfig,
        RemoteDecoratedFileSystemClientFactory remoteFileSystemClientFactory,
        RemoteDecoratedEventLogClientFactory remoteEventLogClientFactory,
        LocalRootMapForDeletionDetectionFactory localSyncRootMapForDeletionDetectionFactory,
        ILocalVolumeInfoProvider localVolumeInfoProvider,
        ILocalFileSystemClientFactory localUndecoratedFileSystemClientFactory,
        ILocalEventLogClientFactory localUndecoratedEventLogClientFactory,
        IRootDeletionHandler syncRootDeletionHandler,
        ISyncFolderStructureProtector folderStructureProtector,
        IFileConsistencyGuardFactory fileConsistencyGuardFactory,
        IScheduler scheduler,
        IClock clock,
        ILoggerFactory loggerFactory,
        IErrorCounter errorCounter)
    {
        _appConfig = appConfig;
        _remoteFileSystemClientFactory = remoteFileSystemClientFactory;
        _remoteEventLogClientFactory = remoteEventLogClientFactory;
        _localSyncRootMapForDeletionDetectionFactory = localSyncRootMapForDeletionDetectionFactory;
        _localVolumeInfoProvider = localVolumeInfoProvider;
        _localUndecoratedFileSystemClientFactory = localUndecoratedFileSystemClientFactory;
        _localUndecoratedEventLogClientFactory = localUndecoratedEventLogClientFactory;
        _syncRootDeletionHandler = syncRootDeletionHandler;
        _folderStructureProtector = folderStructureProtector;
        _fileConsistencyGuardFactory = fileConsistencyGuardFactory;
        _scheduler = scheduler;
        _clock = clock;
        _loggerFactory = loggerFactory;
        _errorCounter = errorCounter;
    }

    public async Task<SyncAgent> GetSyncAgentAsync(IReadOnlyCollection<RemoteToLocalMapping> mappings, CancellationToken cancellationToken = default)
    {
        mappings = mappings.Where(m => SupportedMappingTypes.Contains(m.Type)).ToArray().AsReadOnly();

        var localAdapterSettings = new LocalAdapterSettings
        {
            TempFolderName = _appConfig.FolderNames.TempFolderName,
            BackupFolderName = _appConfig.FolderNames.BackupFolderName,
            TrashFolderName = _appConfig.FolderNames.TrashFolderName,
            EditConflictNamePattern = EditConflictNamePattern,
            DeletedNamePattern = DeletedNamePattern,
        };

        var identitySource = new ConcurrentIdentitySource();

        var specialFolderNames = ImmutableArray.Create(
            localAdapterSettings.BackupFolderName,
            localAdapterSettings.TempFolderName);

        var remoteAdapterDatabase = new RemoteAdapterDatabase(new DatabaseConfig(Path.Combine(_appConfig.AppDataPath, "RemoteAdapter.sqlite")));
        var localAdapterDatabase = new LocalAdapterDatabase(new DatabaseConfig(Path.Combine(_appConfig.AppDataPath, "LocalAdapter.sqlite")));
        var syncEngineDatabase = new SyncEngineDatabase(new DatabaseConfig(Path.Combine(_appConfig.AppDataPath, "SyncEngine.sqlite")));

        var remoteEventLogClient = await _remoteEventLogClientFactory
            .GetClientAsync(mappings, remoteAdapterDatabase.PropertyRepository, cancellationToken)
            .ConfigureAwait(false);

        var remoteFileSystemClient = _remoteFileSystemClientFactory.GetClient(mappings);

        var fileUploadAbortionStrategy = new FileUploadAbortionStrategy(_loggerFactory.CreateLogger<FileUploadAbortionStrategy>());

        var remoteAdapter = new GenericAdapter<long, string>(
            _loggerFactory,
            Replica.Remote,
            _appConfig,
            _scheduler,
            remoteAdapterDatabase.AdapterTreeRepository,
            remoteAdapterDatabase.NodeLinkRepository,
            remoteAdapterDatabase.DirtyTreeRepository,
            remoteAdapterDatabase.StateMaintenanceTreeRepository,
            remoteAdapterDatabase.DetectedUpdateRepository,
            remoteAdapterDatabase.PropertyRepository,
            remoteAdapterDatabase,
            identitySource,
            new ConcurrentIdentitySource(),
            new EvenIdentitySource(),
            new FileNameFactory<long>(TempFileNamePattern),
            specialFolderNames,
            _appConfig.MaxRemoteFileAccessRetryInterval,
            _appConfig.MaxFileRevisionCreationInterval,
            minDelayBeforeFileUpload: TimeSpan.Zero,
            remoteFileSystemClient,
            remoteEventLogClient,
            _clock,
            _errorCounter);

        var localFileSystemClient = new LocalDecoratedFileSystemClientFactory(
                _localVolumeInfoProvider,
                _loggerFactory,
                _localUndecoratedFileSystemClientFactory,
                fileUploadAbortionStrategy,
                _folderStructureProtector)
            .GetClient(mappings, localAdapterSettings);

        var localEventLogClient =
            new LocalDecoratedEventLogClientFactory(
                    _loggerFactory,
                    _localUndecoratedEventLogClientFactory,
                    _localSyncRootMapForDeletionDetectionFactory,
                    fileUploadAbortionStrategy,
                    _syncRootDeletionHandler)
                .GetClient(mappings);

        var localAdapter = new GenericAdapter<long, long>(
            _loggerFactory,
            Replica.Local,
            _appConfig,
            _scheduler,
            localAdapterDatabase.AdapterTreeRepository,
            localAdapterDatabase.NodeLinkRepository,
            localAdapterDatabase.DirtyTreeRepository,
            localAdapterDatabase.StateMaintenanceTreeRepository,
            localAdapterDatabase.DetectedUpdateRepository,
            localAdapterDatabase.PropertyRepository,
            localAdapterDatabase,
            identitySource,
            new ConcurrentIdentitySource(),
            new OddIdentitySource(),
            new FileNameFactory<long>(TempFileNamePattern),
            specialFolderNames,
            _appConfig.MaxLocalFileAccessRetryInterval,
            maxFileRevisionCreationInterval: TimeSpan.Zero,
            _appConfig.MinDelayBeforeFileUpload,
            localFileSystemClient,
            localEventLogClient,
            _clock,
            _errorCounter);

        var serialScheduler = new SerialScheduler();
        var syncEngine = new SyncEngine<long>(
            _appConfig.MaxNumberOfConcurrentFileTransfers,
            _loggerFactory,
            remoteAdapter,
            localAdapter,
            syncEngineDatabase.SyncedTreeRepository,
            syncEngineDatabase.LocalUpdateTreeRepository,
            syncEngineDatabase.RemoteUpdateTreeRepository,
            syncEngineDatabase.PropagationTreeRepository,
            syncEngineDatabase.LocalSyncedUpdateRepository,
            syncEngineDatabase.PropertyRepository,
            syncEngineDatabase,
            identitySource,
            new ConcurrentIdentitySource(),
            new FileNameFactory<long>(NameClashConflictNamePattern),
            new FileNameFactory<long>(DeleteConflictNamePattern),
            new FileNameFactory<long>(TempUniqueNamePattern),
            serialScheduler);

        var stateConsistencyGuard = new StateConsistencyGuard<long>(
            syncEngineDatabase.SyncedTreeRepository,
            syncEngineDatabase.LocalUpdateTreeRepository,
            syncEngineDatabase.RemoteUpdateTreeRepository,
            syncEngineDatabase,
            _loggerFactory);

        var fileConsistencyGuard = _fileConsistencyGuardFactory.Create(
            mappings,
            localAdapter,
            remoteAdapter,
            localAdapter.SyncScheduler,
            localAdapterDatabase,
            remoteAdapter.SyncScheduler,
            remoteAdapterDatabase,
            remoteAdapter);

        return new SyncAgent(
            remoteAdapter,
            localAdapter,
            syncEngine,
            remoteAdapterDatabase,
            localAdapterDatabase,
            syncEngineDatabase,
            stateConsistencyGuard,
            fileConsistencyGuard,
            _scheduler,
            _clock,
            _errorCounter,
            _loggerFactory.CreateLogger<SyncAgent>());
    }
}
