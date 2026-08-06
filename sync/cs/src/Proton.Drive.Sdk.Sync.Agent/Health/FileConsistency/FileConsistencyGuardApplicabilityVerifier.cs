using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Client;
using Proton.Drive.Sdk.Sync.Client.Health;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Agent.Health.FileConsistency;

internal sealed class FileConsistencyGuardApplicabilityVerifier
{
    private const string LocalAdapterDatabaseFileName = "LocalAdapter.sqlite";
    private static readonly TimeSpan RemoteVerificationPeriod = TimeSpan.FromHours(4);

    private readonly AppConfig _appConfig;
    private readonly LocalFeatureFlags _localFeatureFlags;
    private readonly IDriveHealthClient _driveHealthClient;
    private readonly IClock _clock;
    private readonly ILogger<FileConsistencyGuardApplicabilityVerifier> _logger;

    private readonly DateTime _notApplicableIfCreatedAfter;

    private ApplicabilityVerdict _lastRemoteVerificationResult;
    private DateTime? _lastRemoteVerificationTime = DateTime.MinValue;

    public FileConsistencyGuardApplicabilityVerifier(
        AppConfig appConfig,
        LocalFeatureFlags localFeatureFlags,
        IDriveHealthClient driveHealthClient,
        IClock clock,
        ILogger<FileConsistencyGuardApplicabilityVerifier> logger)
    {
        _appConfig = appConfig;
        _localFeatureFlags = localFeatureFlags;
        _driveHealthClient = driveHealthClient;
        _clock = clock;
        _logger = logger;

        _notApplicableIfCreatedAfter = appConfig.FileConsistencyGuardNotApplicableSince.UtcDateTime;
    }

    public async Task<ApplicabilityVerdict> ExecuteAsync(string clientInstanceId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("File consistency guard: Verifying applicability");

        var verdict = await VerifyRemotelyWithCachingAsync(clientInstanceId, cancellationToken).ConfigureAwait(false);

        if (verdict is not ApplicabilityVerdict.Applicable && _localFeatureFlags.FileConsistencyGuardRemoteApplicabilityForceEnabled)
        {
            verdict = ApplicabilityVerdict.Applicable;
            _logger.LogWarning("File consistency guard: Applicability overridden locally");
        }

        if (verdict is not ApplicabilityVerdict.CheckFailed)
        {
            _logger.LogInformation("File consistency guard: {ApplicabilityVerdict}", verdict);
        }

        return verdict;
    }

    public ApplicabilityVerdict VerifyLocally()
    {
        _logger.LogInformation("File consistency guard: Verifying applicability locally");

        var verdict = VerifyLocalAdapterDatabaseCreationTime();

        if (verdict is not ApplicabilityVerdict.CheckFailed)
        {
            _logger.LogInformation("File consistency guard: {ApplicabilityVerdict} locally", verdict);
        }

        return verdict;
    }

    private ApplicabilityVerdict VerifyLocalAdapterDatabaseCreationTime()
    {
        var databaseFilePath = Path.Combine(_appConfig.AppDataPath, LocalAdapterDatabaseFileName);

        try
        {
            var fileCreationTime = File.GetCreationTimeUtc(databaseFilePath);

            return fileCreationTime < _notApplicableIfCreatedAfter ? ApplicabilityVerdict.Applicable : ApplicabilityVerdict.NotApplicable;
        }
        catch (Exception ex) when (ex.IsFileAccessException())
        {
            _logger.LogWarning("File consistency guard: Applicability verification failed: {ErrorMessage}", ex.CombinedMessage());

            return ApplicabilityVerdict.CheckFailed;
        }
    }

    private async Task<ApplicabilityVerdict> VerifyRemotelyWithCachingAsync(string clientInstanceId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        if (now < _lastRemoteVerificationTime + RemoteVerificationPeriod)
        {
            _logger.LogInformation("File consistency guard: Using cached remote applicability verification result");
            return _lastRemoteVerificationResult;
        }

        var result = await VerifyRemotelyAsync(clientInstanceId, cancellationToken).ConfigureAwait(false);

        if (result is not ApplicabilityVerdict.CheckFailed)
        {
            _lastRemoteVerificationTime = _clock.UtcNow;
            _lastRemoteVerificationResult = result;
        }

        return result;
    }

    private async Task<ApplicabilityVerdict> VerifyRemotelyAsync(string clientInstanceId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _driveHealthClient.GetFileConsistencyCheckIsRequiredAsync(clientInstanceId, cancellationToken).ConfigureAwait(false);

            return result ? ApplicabilityVerdict.Applicable : ApplicabilityVerdict.NotApplicable;
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogWarning("File consistency guard: Applicability verification failed: {ErrorMessage}", ex.CombinedMessage());

            return ApplicabilityVerdict.CheckFailed;
        }
    }
}
