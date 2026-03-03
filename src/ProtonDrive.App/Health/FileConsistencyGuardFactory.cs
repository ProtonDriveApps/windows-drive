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
        ITransactedScheduler localAdapterSyncScheduler,
        LocalAdapterDatabase localAdapterDatabase,
        ITransactedScheduler remoteAdapterSyncScheduler,
        RemoteAdapterDatabase remoteAdapterDatabase)
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
            _errorCounter,
            _loggerFactory.CreateLogger<FileConsistencyGuard>());
    }
}
