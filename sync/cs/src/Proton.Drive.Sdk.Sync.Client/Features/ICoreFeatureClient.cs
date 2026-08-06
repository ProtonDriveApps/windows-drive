namespace Proton.Drive.Sdk.Sync.Client.Features;

public interface ICoreFeatureClient
{
    Task<bool?> IsFeatureEnabledAsync(string featureCode, CancellationToken cancellationToken);
}
