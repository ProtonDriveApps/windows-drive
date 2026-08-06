namespace Proton.Drive.Sdk.Sync.Shared.Authentication;

public interface ISessionStateAware
{
    void OnSessionStateChanged(SessionState value);
}
