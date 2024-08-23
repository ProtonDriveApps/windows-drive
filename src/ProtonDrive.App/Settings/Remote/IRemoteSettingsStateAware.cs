namespace ProtonDrive.App.Settings.Remote;

public interface IRemoteSettingsStateAware
{
    void OnRemoteSettingsChanged(bool isEnabled);
}
