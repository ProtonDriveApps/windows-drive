using ProtonDrive.Shared.Features;

namespace ProtonDrive.Client;

internal static class FeatureFlagProviderExtensions
{
    public static async Task<bool> UploadChecksumVerificationIsEnabledAsync(this IFeatureFlagProvider featureFlagProvider, CancellationToken cancellationToken)
    {
        return !await featureFlagProvider.IsEnabledAsync(Feature.DriveUploadVerificationDisabled, cancellationToken).ConfigureAwait(false);
    }
}
