using Proton.Drive.Shared.Features;

namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

public interface IRemoteFileSystemClientFactory
{
    /// <summary>
    /// Creates a file system client that can dynamically switch between legacy and SDK implementations
    /// based on feature flags. The returned client implements <see cref="IFeatureFlagsAware"/> and
    /// must be registered with the feature flag system to receive updates.
    /// </summary>
    /// <param name="parameters">The file system client parameters.</param>
    /// <returns>A switchable file system client that implements <see cref="IFeatureFlagsAware"/>.</returns>
    IFileSystemClient<string> CreateClient(FileSystemClientParameters parameters);
}
