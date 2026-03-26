using ProtonDrive.Shared.Features;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

internal static class FeatureFlagProviderExtensions
{
    public static async Task<bool> DownloadChecksumVerificationIsEnabledAsync(this IFeatureFlagProvider featureFlagProvider, CancellationToken cancellationToken)
    {
        return !await featureFlagProvider.IsEnabledAsync(Feature.DriveDownloadVerificationDisabled, cancellationToken).ConfigureAwait(false);
    }
}
