namespace Proton.Drive.Shared.Features;

public interface IFeatureFlagProvider
{
    Task<bool> IsEnabledAsync(Feature feature, CancellationToken cancellationToken);
}
