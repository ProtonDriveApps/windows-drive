namespace ProtonDrive.App.Authentication;

public enum SigningInStatus
{
    None,
    WaitingForAuthenticationPassword,
    WaitingForSecondFactorCode,
    WaitingForDataPassword,
    Authenticating,
}
