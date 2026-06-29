namespace Proton.Drive.Sdk.Sync.Agent.Settings.Remote;

public interface IRemoteSettingsStateAware
{
    void OnRemoteSettingsStateChanged(RemoteSettingsStatus status);
}
