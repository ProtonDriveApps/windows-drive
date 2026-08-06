namespace Proton.Drive.Sdk.Sync.Shared.Authentication;

public enum SigningInStatus
{
    None,
    WaitingForAuthenticationPassword,
    WaitingForSecondFactorAuthentication,
    WaitingForDataPassword,
    Authenticating,
}
