using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Shared.Features;

public interface IFeatureFlagProvider
{
    Task<bool> IsEnabledAsync(Feature feature, CancellationToken cancellationToken);
}
