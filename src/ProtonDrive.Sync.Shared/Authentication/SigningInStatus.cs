namespace ProtonDrive.App.Authentication;

public enum SigningInStatus
{
    None,
    WaitingForAuthenticationPassword,
    WaitingForSecondFactorAuthentication,
    WaitingForDataPassword,
    Authenticating,
}
