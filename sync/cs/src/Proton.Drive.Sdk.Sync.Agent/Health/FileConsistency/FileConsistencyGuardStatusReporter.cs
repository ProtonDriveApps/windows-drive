using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Client;
using Proton.Drive.Sdk.Sync.Client.Health;
using Proton.Drive.Shared.Client;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Agent.Health.FileConsistency;

internal sealed class FileConsistencyGuardStatusReporter
{
    private readonly LocalFeatureFlags _localFeatureFlags;
    private readonly IDriveHealthClient _driveHealthClient;
    private readonly IErrorCounter _errorCounter;
    private readonly ILogger<FileConsistencyGuardStatusReporter> _logger;

    public FileConsistencyGuardStatusReporter(
        LocalFeatureFlags localFeatureFlags,
        IDriveHealthClient driveHealthClient,
        IErrorCounter errorCounter,
        ILogger<FileConsistencyGuardStatusReporter> logger)
    {
        _localFeatureFlags = localFeatureFlags;
        _driveHealthClient = driveHealthClient;
        _errorCounter = errorCounter;
        _logger = logger;
    }

    public async Task<bool> ReportStartAsync(string clientInstanceId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("File consistency guard: Starting");

        try
        {
            await _driveHealthClient.StartFileConsistencyCheckAsync(clientInstanceId, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("File consistency guard: Started");

            return true;
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogWarning("File consistency guard: Starting failed: {ErrorMessage}", ex.CombinedMessage());

            if (_localFeatureFlags.FileConsistencyGuardRemoteApplicabilityForceEnabled)
            {
                _logger.LogWarning("File consistency guard: Starting overridden locally");
                return true;
            }

            _errorCounter.Add(ErrorScope.DataIntegrity, ex);

            return false;
        }
    }

    public async Task<bool> ReportCompletionAsync(string clientInstanceId, FileConsistencyCheckResult result, CancellationToken cancellationToken)
    {
        _logger.LogInformation("File consistency guard: Finishing");

        try
        {
            await _driveHealthClient.FinishFileConsistencyCheckAsync(clientInstanceId, result, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("File consistency guard: Finished");

            return true;
        }
        catch (ApiException ex) when (ex.ResponseCode is ResponseCode.IncompatibleState)
        {
            _logger.LogWarning("File consistency guard: Already finished: {ErrorMessage}", ex.Message);

            return true;
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogWarning("File consistency guard: Finishing failed: {ErrorMessage}", ex.CombinedMessage());

            _errorCounter.Add(ErrorScope.DataIntegrity, ex);

            return false;
        }
    }
}
