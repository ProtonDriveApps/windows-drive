namespace ProtonDrive.App.Settings.Remote;

public enum RemoteSettingsStatus
{
    /// <summary>
    /// Session is not started
    /// </summary>
    None,

    /// <summary>
    /// Setting up remote settings
    /// </summary>
    SettingUp,

    /// <summary>
    /// Setting up remote settings has succeeded
    /// </summary>
    Succeeded,

    /// <summary>
    /// Setting up remote settings has failed
    /// </summary>
    Failed,
}
