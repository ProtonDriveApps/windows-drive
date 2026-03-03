using Microsoft.Extensions.Logging;
using ProtonDrive.App.Settings;
using ProtonDrive.Client.Health;
using ProtonDrive.DataAccess;
using ProtonDrive.DataAccess.Databases;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Devices;
using ProtonDrive.Shared.Features;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Shared.Telemetry;
using ProtonDrive.Sync.Adapter;
using ProtonDrive.Sync.Agent.Health;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Adapters;

namespace ProtonDrive.App.Health;

internal sealed class FileConsistencyGuardFactory : IFileConsistencyGuardFactory
{
    private readonly AppConfig _appConfig;
    private readonly IRepository<FileConsistencyGuardSettings> _settingsRepository;
    private readonly IFeatureFlagProvider _featureFlagProvider;
    private readonly IClientInstanceIdentityProvider _clientInstanceIdentityProvider;
    private readonly FileConsistencyGuardApplicabilityVerifier _applicabilityVerifier;
    private readonly FileConsistencyGuardStatusReporter _statusReporter;
    private readonly IRemoteFileMetadataProvider _remoteFileHashProvider;
    private readonly IErrorCounter _errorCounter;
    private readonly ILoggerFactory _loggerFactory;

    public FileConsistencyGuardFactory(
        AppConfig appConfig,
        IRepository<FileConsistencyGuardSettings> settingsRepository,
        IFeatureFlagProvider featureFlagProvider,
        IClientInstanceIdentityProvider clientInstanceIdentityProvider,
        FileConsistencyGuardApplicabilityVerifier applicabilityVerifier,
        FileConsistencyGuardStatusReporter statusReporter,
        IRemoteFileMetadataProvider remoteFileHashProvider,
        IErrorCounter errorCounter,
        ILoggerFactory loggerFactory)
    {
        _appConfig = appConfig;
        _settingsRepository = settingsRepository;
        _featureFlagProvider = featureFlagProvider;
        _clientInstanceIdentityProvider = clientInstanceIdentityProvider;
        _applicabilityVerifier = applicabilityVerifier;
        _statusReporter = statusReporter;
        _remoteFileHashProvider = remoteFileHashProvider;
        _errorCounter = errorCounter;
        _loggerFactory = loggerFactory;
    }

    public IFileConsistencyGuard Create(
        IReadOnlyCollection<RemoteToLocalMapping> mappings,
        IFileRevisionProvider<long> localFileRevisionProvider,
        IFileRevisionProvider<long> remoteFileRevisionProvider,
        ITransactedScheduler localAdapterSyncScheduler,
        LocalAdapterDatabase localAdapterDatabase,
        ITransactedScheduler remoteAdapterSyncScheduler,
        RemoteAdapterDatabase remoteAdapterDatabase,
        IManagedAdapter<long> remoteAdapter)
    {
        var database = new FileConsistencyGuardDatabase(new DatabaseConfig(Path.Combine(_appConfig.AppDataPath, "FileConsistencyGuard.sqlite")));

        var dataInitializer = new FileConsistencyGuardDataInitializer(
            database,
            localAdapterSyncScheduler,
            localAdapterDatabase,
            remoteAdapterSyncScheduler,
            remoteAdapterDatabase,
            _loggerFactory.CreateLogger<FileConsistencyGuardDataInitializer>());

        var localFileMetadataProvider = new LocalFileMetadataUpdater(
            _appConfig,
            database,
            localFileRevisionProvider,
            _errorCounter,
            _loggerFactory.CreateLogger<LocalFileMetadataUpdater>());

        var remoteFileMetadataRefresher = new RemoteFileMetadataRefresher(
            database,
            remoteAdapterSyncScheduler,
            remoteAdapterDatabase,
            _loggerFactory.CreateLogger<RemoteFileMetadataRefresher>());

        var remoteFileMetadataProvider = new RemoteFileMetadataUpdater(
            database,
            mappings,
            _remoteFileHashProvider,
            _errorCounter,
            _loggerFactory.CreateLogger<RemoteFileMetadataUpdater>());

        var remoteFileMetadataCalculator = new RemoteFileMetadataCalculator(
            database,
            remoteFileRevisionProvider,
            _errorCounter,
            _loggerFactory.CreateLogger<RemoteFileMetadataCalculator>());

        var localFileMetadataValidator = new LocalFileMetadataValidator(
            localFileRevisionProvider,
            _errorCounter,
            _loggerFactory.CreateLogger<LocalFileMetadataValidator>());

        var remoteFileDownloadTrigger = new RemoteFileDownloadTrigger(
            remoteAdapter,
            _errorCounter,
            _loggerFactory.CreateLogger<RemoteFileDownloadTrigger>());

        var fileSanitizer = new FileConsistencyGuardFileSanitizer(
            database,
            localFileMetadataValidator,
            remoteFileDownloadTrigger,
            _loggerFactory.CreateLogger<FileConsistencyGuardFileSanitizer>());

        var completionVerifier = new FileConsistencyGuardCompletionVerifier(
            database,
            _loggerFactory.CreateLogger<FileConsistencyGuardCompletionVerifier>());

        return new FileConsistencyGuard(
            _appConfig,
            _settingsRepository,
            _clientInstanceIdentityProvider,
            _featureFlagProvider,
            database,
            _applicabilityVerifier,
            dataInitializer,
            _statusReporter,
            localFileMetadataProvider,
            remoteFileMetadataRefresher,
            remoteFileMetadataProvider,
            remoteFileMetadataCalculator,
            fileSanitizer,
            completionVerifier,
            _errorCounter,
            _loggerFactory.CreateLogger<FileConsistencyGuard>());
    }
}
