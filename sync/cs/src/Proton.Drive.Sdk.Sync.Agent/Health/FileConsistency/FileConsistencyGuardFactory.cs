using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Adapter;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Client.Health;
using Proton.Drive.Sdk.Sync.DataAccess;
using Proton.Drive.Sdk.Sync.DataAccess.Databases;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Adapters;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Devices;
using Proton.Drive.Shared.Features;
using Proton.Drive.Shared.Repository;
using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Agent.Health.FileConsistency;

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
