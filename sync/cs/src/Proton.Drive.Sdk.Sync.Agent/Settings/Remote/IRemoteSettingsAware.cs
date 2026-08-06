namespace Proton.Drive.Sdk.Sync.Agent.Settings.Remote;

public interface IRemoteSettingsAware
{
    void OnRemoteSettingsChanged(RemoteSettings settings);
}
