using Proton.Drive.Sdk.Sync.Agent.Volumes;

namespace Proton.Drive.App.Photos;

public enum PhotosFeatureStatus
{
    /// <summary>
    /// One of the following conditions is met:
    /// <list type="bullet">
    /// <item>Related services are not ready. In particular, <see cref="MainVolumeService"/> has not yet succeeded setup.</item>
    /// <item>Photos is not configured to be used and there is no active Photo volume.</item>
    /// </list>
    /// </summary>
    Idle,

    /// <summary>
    /// Onboarding to Photos feature has not completed.
    /// </summary>
    Onboarding,

    /// <summary>
    /// Setting up Photo volume, migrating data if needed.
    /// </summary>
    SettingUp,

    /// <summary>
    /// Photos feature is ready to use.
    /// </summary>
    Ready,

    /// <summary>
    /// Photos feature is read-only.
    /// </summary>
    /// <remarks>
    /// Feature is temporary limited due to remote kill switch is enabled.
    /// Photos folder mapping setup fails, but mapping is not automatically deleted by the app.
    /// </remarks>
    ReadOnly,

    /// <summary>
    /// Photos feature is disabled.
    /// </summary>
    /// <remarks>
    /// Photos folder mapping is automatically deleted by the app.
    /// </remarks>
    Disabled,

    /// <summary>
    /// Photos feature is hidden.
    /// </summary>
    Hidden,
}
