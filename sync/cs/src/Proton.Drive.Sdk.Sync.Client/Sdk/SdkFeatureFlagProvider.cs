using Microsoft.Extensions.Logging;
using Proton.Drive.Shared.Features;

namespace Proton.Drive.Sdk.Sync.Client.Sdk;

internal sealed class SdkFeatureFlagProvider : Proton.Sdk.IFeatureFlagProvider
{
    private readonly IFeatureFlagProvider _featureFlagProvider;
    private readonly ILogger<SdkFeatureFlagProvider> _logger;

    public SdkFeatureFlagProvider(IFeatureFlagProvider featureFlagProvider, ILogger<SdkFeatureFlagProvider> logger)
    {
        _featureFlagProvider = featureFlagProvider;
        _logger = logger;
    }

    public async Task<bool> IsEnabledAsync(string flagName, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Feature>(flagName, ignoreCase: true, out var feature))
        {
            _logger.LogWarning("Feature flag '{FlagName}' is not recognized.", flagName);
            return false;
        }

        return await _featureFlagProvider.IsEnabledAsync(feature, cancellationToken).ConfigureAwait(false);
    }
}
