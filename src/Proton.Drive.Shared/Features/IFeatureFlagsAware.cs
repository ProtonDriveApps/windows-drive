namespace Proton.Drive.Shared.Features;

/// <summary>
/// Interface for components that need to be notified when feature flags change.
/// </summary>
public interface IFeatureFlagsAware
{
    /// <summary>
    /// Called when feature flags have changed.
    /// </summary>
    /// <param name="features">The dictionary of features with their current enabled state.</param>
    void OnFeatureFlagsChanged(IReadOnlyDictionary<Feature, bool> features);
}
