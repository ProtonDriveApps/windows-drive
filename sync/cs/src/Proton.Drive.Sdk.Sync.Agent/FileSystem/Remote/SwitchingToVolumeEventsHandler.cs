using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.DataAccess;
using Proton.Drive.Sdk.Sync.DataAccess.Databases;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Features;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Remote;

internal sealed class SwitchingToVolumeEventsHandler : ISwitchingToVolumeEventsHandler
{
    private readonly AppConfig _appConfig;
    private readonly IFeatureFlagProvider _featureFlagProvider;
    private readonly ILogger<SwitchingToVolumeEventsHandler> _logger;

    public SwitchingToVolumeEventsHandler(
        AppConfig appConfig,
        IFeatureFlagProvider featureFlagProvider,
        ILogger<SwitchingToVolumeEventsHandler> logger)
    {
        _appConfig = appConfig;
        _featureFlagProvider = featureFlagProvider;
        _logger = logger;
    }

    public bool HasSwitched { get; private set; }

    public async Task<bool> TrySwitchAsync(IReadOnlyCollection<RemoteToLocalMapping> activeMappings, CancellationToken cancellationToken)
    {
        var result = await InternalTrySwitchAsync(activeMappings, cancellationToken).ConfigureAwait(false);

        HasSwitched = result;

        return result;
    }

    private async Task<bool> InternalTrySwitchAsync(IReadOnlyCollection<RemoteToLocalMapping> activeMappings, CancellationToken cancellationToken)
    {
        var database = new RemoteAdapterDatabase(new DatabaseConfig(Path.Combine(_appConfig.AppDataPath, "RemoteAdapter.sqlite")));

        var migration = new ShareToVolumeEventAnchorMigration(database.PropertyRepository);

        database.Open();

        try
        {
            var transaction = database.Connection.BeginTransaction();

            using (transaction)
            {
                var failed = false;
                try
                {
                    if (!migration.IsRequired())
                    {
                        _logger.LogInformation("Switching to volume based events is not required, already switched");

                        return true;
                    }

                    var shareToVolumeMapping = GetShareToVolumeMapping(activeMappings);

                    if (!migration.TryApply(shareToVolumeMapping))
                    {
                        var forceMigrationEnabled = await _featureFlagProvider
                            .IsEnabledAsync(Feature.DriveWindowsForceMigrationToVolumeEvents, cancellationToken)
                            .ConfigureAwait(false);

                        if (forceMigrationEnabled)
                        {
                            _logger.LogInformation("Forcing migration to volume based events...");
                            migration.ForceMigration();
                        }
                        else
                        {
                            _logger.LogWarning("Switching to volume based events is not possible");
                            return false;
                        }
                    }

                    _logger.LogInformation("Switching to volume based events succeeded");

                    return true;
                }
                catch
                {
                    failed = true;
                    transaction.Rollback();

                    throw;
                }
                finally
                {
                    if (!failed)
                    {
                        transaction.Commit();
                    }
                }
            }
        }
        finally
        {
            database.Close();
        }
    }

    private static IReadOnlyDictionary<string, string> GetShareToVolumeMapping(IEnumerable<RemoteToLocalMapping> activeMappings)
    {
        return activeMappings
            .Where(x => !string.IsNullOrEmpty(x.Remote.VolumeId) && !string.IsNullOrEmpty(x.Remote.ShareId))
            .Select(
                x => new
                {
                    ShareId = x.Remote.ShareId ?? throw new InvalidOperationException(),
                    VolumeId = x.Remote.VolumeId ?? throw new InvalidOperationException(),
                })
            .DistinctBy(x => x.VolumeId + x.ShareId)
            .ToDictionary(x => x.ShareId, y => y.VolumeId);
    }
}
