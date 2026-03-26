namespace ProtonDrive.Client.Features;

public interface ICoreFeatureClient
{
    Task<bool?> IsFeatureEnabledAsync(string featureCode, CancellationToken cancellationToken);
}
