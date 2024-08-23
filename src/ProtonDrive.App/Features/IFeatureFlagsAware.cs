using System.Collections.Generic;
using ProtonDrive.Shared.Features;

namespace ProtonDrive.App.Features;

public interface IFeatureFlagsAware
{
    void OnFeatureFlagsChanged(IReadOnlyCollection<(Feature Feature, bool IsEnabled)> features);
}
