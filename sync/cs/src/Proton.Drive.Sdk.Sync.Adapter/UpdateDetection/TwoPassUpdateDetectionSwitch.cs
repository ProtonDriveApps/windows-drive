using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Shared.Features;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection;

internal sealed class TwoPassUpdateDetectionSwitch(IFeatureFlagProvider featureFlagProvider, Replica replica, ILogger<TwoPassUpdateDetectionSwitch> logger)
{
    private readonly IFeatureFlagProvider _featureFlagProvider = featureFlagProvider;
    private readonly Replica _replica = replica;
    private readonly ILogger<TwoPassUpdateDetectionSwitch> _logger = logger;

    private volatile bool _isDisabled;

    public bool IsDisabled => _isDisabled;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var isDisabled = await _featureFlagProvider
            .IsEnabledAsync(Feature.DriveWindowsTwoPassUpdateDetectionDisabled, cancellationToken)
            .ConfigureAwait(false);

        if (isDisabled == _isDisabled)
        {
            return;
        }

        _isDisabled = isDisabled;

        _logger.LogInformation("{Replica} two-pass update detection is {State}", _replica, isDisabled ? "disabled" : "enabled");
    }
}
