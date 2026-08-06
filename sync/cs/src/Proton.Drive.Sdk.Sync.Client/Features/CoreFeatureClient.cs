using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Client.Features.Contracts;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Client.Features;

internal sealed class CoreFeatureClient : ICoreFeatureClient
{
    private readonly ICoreFeatureApiClient _apiClient;
    private readonly ILogger<CoreFeatureClient> _logger;

    public CoreFeatureClient(ICoreFeatureApiClient apiClient, ILogger<CoreFeatureClient> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<bool?> IsFeatureEnabledAsync(string featureCode, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _apiClient.GetFeatureAsync(featureCode, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

            var feature = response.Feature;

            LogFeatureStatus(feature, featureCode);

            return feature.Value == 1;
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogWarning("Core feature \"{FeatureCode}\" check failed: {ErrorMessage}", featureCode, ex.CombinedMessage());
            return null;
        }
    }

    private void LogFeatureStatus(CoreFeature? feature, string featureCode)
    {
        if (feature is null)
        {
            _logger.LogInformation("Core feature \"{FeatureCode}\" does not exist", featureCode);
        }
        else
        {
            _logger.LogInformation("Core feature \"{FeatureCode}\" is \"{Value}\"", featureCode, feature.Value);
        }
    }
}
