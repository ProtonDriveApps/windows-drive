using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.FileSystem.Local;
using ProtonDrive.App.FileSystem.Remote;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Sanitization;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.DataAccess;
using ProtonDrive.DataAccess.Databases;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Telemetry;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Adapter;
using ProtonDrive.Sync.Agent.Validation;
using ProtonDrive.Sync.Engine;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Windows.FileSystem.Client;

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
    private readonly FileSanitizationProvider _fileSanitizerProvider;
    private readonly ILocalVolumeInfoProvider _localVolumeInfoProvider;
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly IRootDeletionHandler _syncRootDeletionHandler;
    private readonly IScheduler _scheduler;
    private readonly IClock _clock;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IErrorCounter _errorCounter;

    public SyncAgentFactory(
        AppConfig appConfig,
        RemoteDecoratedFileSystemClientFactory remoteFileSystemClientFactory,
        RemoteDecoratedEventLogClientFactory remoteEventLogClientFactory,
        LocalRootMapForDeletionDetectionFactory localSyncRootMapForDeletionDetectionFactory,
        FileSanitizationProvider fileSanitizerProvider,
        ILocalVolumeInfoProvider localVolumeInfoProvider,
        IThumbnailGenerator thumbnailGenerator,
        IRootDeletionHandler syncRootDeletionHandler,
        IScheduler scheduler,
        IClock clock,
        ILoggerFactory loggerFactory,
        IErrorCounter errorCounter)
    {
        _appConfig = appConfig;
        _remoteFileSystemClientFactory = remoteFileSystemClientFactory;
        _remoteEventLogClientFactory = remoteEventLogClientFactory;
        _localSyncRootMapForDeletionDetectionFactory = localSyncRootMapForDeletionDetectionFactory;
        _fileSanitizerProvider = fileSanitizerProvider;
        _localVolumeInfoProvider = localVolumeInfoProvider;
        _thumbnailGenerator = thumbnailGenerator;
        _syncRootDeletionHandler = syncRootDeletionHandler;
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
        var transferDatabase = new FileTransferDatabase(new DatabaseConfig(Path.Combine(_appConfig.AppDataPath, "FileTransfers.sqlite")));

        var remoteEventLogClient = await _remoteEventLogClientFactory
            .GetClientAsync(mappings, remoteAdapterDatabase.PropertyRepository, cancellationToken)
            .ConfigureAwait(false);

        var remoteFileSystemClient = _remoteFileSystemClientFactory.GetClient(mappings, transferDatabase.RevisionUploadAttemptRepository);

        var fileUploadAbortionStrategy = new FileUploadAbortionStrategy();

        var remoteAdapter = new GenericAdapter<long, string>(
            _loggerFactory,
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
            minDelayBeforeFileUpload: default,
            remoteFileSystemClient,
            remoteEventLogClient,
            _clock,
            _errorCounter);

        var localFileSystemClient = new LocalDecoratedFileSystemClientFactory(
                _localVolumeInfoProvider,
                _loggerFactory,
                () => new ClassicFileSystemClient(_thumbnailGenerator),
                () => new OnDemandHydrationFileSystemClient(_thumbnailGenerator, _loggerFactory),
                fileUploadAbortionStrategy)
            .GetClient(mappings, localAdapterSettings);

        var localEventLogClient =
            new LocalDecoratedEventLogClientFactory(
                    _loggerFactory,
                    entriesFilter => new EventLogClient(entriesFilter, _loggerFactory.CreateLogger<EventLogClient>()),
                    _localSyncRootMapForDeletionDetectionFactory,
                    fileUploadAbortionStrategy,
                    _syncRootDeletionHandler)
                .GetClient(mappings);

        var localAdapter = new GenericAdapter<long, long>(
            _loggerFactory,
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
            maxFileRevisionCreationInterval: default,
            _appConfig.MinDelayBeforeFileUpload,
            localFileSystemClient,
            localEventLogClient,
            _clock,
            _errorCounter);

        var serialScheduler = new SerialScheduler();
        var syncEngine = new SyncEngine<long>(
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

        var fileSanitizer = _fileSanitizerProvider.Create(remoteAdapterDatabase, localAdapterDatabase, mappings, remoteAdapter.TryMarkNodeAsDirtyAsync);

        var stateConsistencyGuard = new StateConsistencyGuard<long>(
            syncEngineDatabase.SyncedTreeRepository,
            syncEngineDatabase.LocalUpdateTreeRepository,
            syncEngineDatabase.RemoteUpdateTreeRepository,
            syncEngineDatabase,
            _loggerFactory);

        return new SyncAgent(
            remoteAdapter,
            localAdapter,
            syncEngine,
            remoteAdapterDatabase,
            localAdapterDatabase,
            syncEngineDatabase,
            transferDatabase,
            fileSanitizer,
            stateConsistencyGuard,
            _scheduler,
            _clock,
            _errorCounter,
            _loggerFactory.CreateLogger<SyncAgent>());
    }
}
