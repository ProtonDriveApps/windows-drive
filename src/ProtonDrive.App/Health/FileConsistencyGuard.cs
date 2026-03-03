using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ProtonDrive.Client.Health;
using ProtonDrive.DataAccess.Databases;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Devices;
using ProtonDrive.Shared.Features;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Shared.Telemetry;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Agent.Health;
using ProtonDrive.Sync.Shared.Health;

namespace ProtonDrive.App.Health;

internal sealed class FileConsistencyGuard : IFileConsistencyGuard
{
    private readonly TimeSpan _retryInterval;

    private readonly IRepository<FileConsistencyGuardSettings> _settingsRepository;
    private readonly IClientInstanceIdentityProvider _clientInstanceIdentityProvider;
    private readonly IFeatureFlagProvider _featureFlagProvider;
    private readonly FileConsistencyGuardDatabase _database;
    private readonly FileConsistencyGuardApplicabilityVerifier _applicabilityVerifier;
    private readonly FileConsistencyGuardDataInitializer _dataInitializer;
    private readonly FileConsistencyGuardStatusReporter _statusReporter;
    private readonly LocalFileMetadataUpdater _localMetadataUpdater;
    private readonly RemoteFileMetadataRefresher _remoteMetadataRefresher;
    private readonly RemoteFileMetadataUpdater _remoteMetadataUpdater;
    private readonly IErrorCounter _errorCounter;
    private readonly ILogger<FileConsistencyGuard> _logger;

    private readonly SingleAction _execution;

    private FileConsistencyGuardSettings _settings = new();

    public FileConsistencyGuard(
        AppConfig appConfig,
        IRepository<FileConsistencyGuardSettings> settingsRepository,
        IClientInstanceIdentityProvider clientInstanceIdentityProvider,
        IFeatureFlagProvider featureFlagProvider,
        FileConsistencyGuardDatabase database,
        FileConsistencyGuardApplicabilityVerifier applicabilityVerifier,
        FileConsistencyGuardDataInitializer dataInitializer,
        FileConsistencyGuardStatusReporter statusReporter,
        LocalFileMetadataUpdater localMetadataUpdater,
        RemoteFileMetadataRefresher remoteMetadataRefresher,
        RemoteFileMetadataUpdater remoteMetadataUpdater,
        IErrorCounter errorCounter,
        ILogger<FileConsistencyGuard> logger)
    {
        _settingsRepository = settingsRepository;
        _clientInstanceIdentityProvider = clientInstanceIdentityProvider;
        _featureFlagProvider = featureFlagProvider;
        _database = database;
        _applicabilityVerifier = applicabilityVerifier;
        _dataInitializer = dataInitializer;
        _statusReporter = statusReporter;
        _localMetadataUpdater = localMetadataUpdater;
        _remoteMetadataRefresher = remoteMetadataRefresher;
        _remoteMetadataUpdater = remoteMetadataUpdater;
        _errorCounter = errorCounter;
        _logger = logger;

        _retryInterval = appConfig.FileConsistencyGuardRetryInterval;
        _execution = _logger.GetSingleActionWithExceptionsLoggingAndCancellationHandling(ExecuteAsync, nameof(FileConsistencyGuard));
    }

    public FileConsistencyGuardStatus Status => _settings.Status;

    public void StartExecuting()
    {
        _ = _execution.RunAsync();
    }

    public Task StopExecutingAsync()
    {
        _execution.Cancel();
        return _execution.WaitForCompletionAsync();
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            LoadSettings();

            while (Status is not FileConsistencyGuardStatus.Finished)
            {
                if (await FeatureIsEnabledAsync(cancellationToken).ConfigureAwait(false) &&
                    await VerifyApplicabilityAsync(cancellationToken).ConfigureAwait(false))
                {
                    await ReportStartAsync(cancellationToken).ConfigureAwait(false);

                    try
                    {
                        _database.Open();

                        await UpdateStatisticsAsync(cancellationToken).ConfigureAwait(false);

                        await InitializeDataAsync(cancellationToken).ConfigureAwait(false);

                        await VerifyConsistencyAsync(cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        _database.Close();
                    }

                    await ReportCompletionAsync(cancellationToken).ConfigureAwait(false);
                }

                await Task.Delay(_retryInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _errorCounter.Add(ErrorScope.DataIntegrity, ex);
            throw;
        }
    }

    private Task<bool> FeatureIsEnabledAsync(CancellationToken cancellationToken)
    {
        return _featureFlagProvider.IsEnabledAsync(Feature.DriveWindowsFileConsistencyGuard, cancellationToken);
    }

    private async Task<bool> VerifyApplicabilityAsync(CancellationToken cancellationToken)
    {
        if (Status is not FileConsistencyGuardStatus.NotStarted and not FileConsistencyGuardStatus.NotApplicable)
        {
            return true;
        }

        var verdict = await _applicabilityVerifier.ExecuteAsync(GetClientInstanceId(), cancellationToken).ConfigureAwait(false);

        var status = verdict switch
        {
            ApplicabilityVerdict.NotApplicable => FileConsistencyGuardStatus.NotApplicable,
            ApplicabilityVerdict.Applicable => FileConsistencyGuardStatus.Applicable,
            ApplicabilityVerdict.CheckFailed => Status,
            _ => throw new InvalidEnumArgumentException(),
        };

        SetStatus(status);

        return status is FileConsistencyGuardStatus.Applicable;
    }

    private async Task ReportStartAsync(CancellationToken cancellationToken)
    {
        if (Status is not FileConsistencyGuardStatus.Applicable)
        {
            return;
        }

        var succeeded = await _statusReporter.ReportStartAsync(GetClientInstanceId(), cancellationToken).ConfigureAwait(false);

        var status = succeeded ? FileConsistencyGuardStatus.Started : Status;

        SetStatus(status);
    }

    private async Task InitializeDataAsync(CancellationToken cancellationToken)
    {
        if (Status is not FileConsistencyGuardStatus.Started)
        {
            return;
        }

        var succeeded = await _dataInitializer.ExecuteAsync(cancellationToken).ConfigureAwait(false);

        var status = succeeded ? FileConsistencyGuardStatus.Initialized : Status;

        SetStatus(status);

        await UpdateStatisticsAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task VerifyConsistencyAsync(CancellationToken cancellationToken)
    {
        if (Status is not FileConsistencyGuardStatus.Initialized)
        {
            return;
        }

        if (await _localMetadataUpdater.ExecuteAsync(cancellationToken).ConfigureAwait(false))
        {
            await UpdateStatisticsAsync(cancellationToken).ConfigureAwait(false);
        }

        if (await _remoteMetadataRefresher.ExecuteAsync(cancellationToken).ConfigureAwait(false))
        {
            await UpdateStatisticsAsync(cancellationToken).ConfigureAwait(false);
        }

        if (await _remoteMetadataUpdater.ExecuteAsync(cancellationToken).ConfigureAwait(false))
        {
            await UpdateStatisticsAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReportCompletionAsync(CancellationToken cancellationToken)
    {
        if (Status is not FileConsistencyGuardStatus.Completed)
        {
            return;
        }

        var fileStatistics = _settings.FileStatistics;
        var inspectedItemCount = fileStatistics
            .Where(x => x.Status is FileConsistencyGuardFileStatus.Consistent or FileConsistencyGuardFileStatus.Inconsistent or FileConsistencyGuardFileStatus.Repaired)
            .Sum(x => x.NumberOfFiles);
        var refreshedItemCount = fileStatistics
            .Where(x => x.Status is FileConsistencyGuardFileStatus.Repaired)
            .Sum(x => x.NumberOfFiles);
        var failedItemCount = fileStatistics
            .Where(x => x.Status is FileConsistencyGuardFileStatus.None or FileConsistencyGuardFileStatus.Inconsistent)
            .Sum(x => x.NumberOfFiles);

        var result = new FileConsistencyCheckResult
        {
            InspectedItemCount = inspectedItemCount,
            RefreshedItemCount = refreshedItemCount,
            FailedItemCount = failedItemCount,
        };

        var succeeded = await _statusReporter.ReportCompletionAsync(GetClientInstanceId(), result, cancellationToken).ConfigureAwait(false);

        var status = succeeded ? FileConsistencyGuardStatus.Finished : Status;

        SetStatus(status);
    }

    private async Task UpdateStatisticsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var newStatistics = (await _database.FileRepository.GetFileStatisticsAsync().ConfigureAwait(false)).ToList();
        var oldStatistics = _settings.FileStatistics;

        if (oldStatistics.Count == newStatistics.Count && oldStatistics.SequenceEqual(newStatistics))
        {
            return;
        }

        _settings.FileStatistics = newStatistics;

        SaveSettings();
    }

    private string GetClientInstanceId()
    {
        if (string.IsNullOrEmpty(_settings.ClientInstanceId))
        {
            _settings.ClientInstanceId = _clientInstanceIdentityProvider.GetClientInstanceId();

            SaveSettings();
        }

        return _settings.ClientInstanceId;
    }

    private void SetStatus(FileConsistencyGuardStatus value)
    {
        _settings.Status = value;

        SaveSettings();
    }

    private void LoadSettings()
    {
        _settings = _settingsRepository.Get() ?? new FileConsistencyGuardSettings();

        _logger.LogInformation("File consistency guard: {Status}", Status);
    }

    private void SaveSettings()
    {
        _settingsRepository.Set(_settings);
    }
}
