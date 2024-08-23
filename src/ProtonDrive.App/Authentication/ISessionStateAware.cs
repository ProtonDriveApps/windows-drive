namespace ProtonDrive.App.Authentication;

public interface ISessionStateAware
{
    void OnSessionStateChanged(SessionState value);
}
