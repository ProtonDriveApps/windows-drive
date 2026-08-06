namespace Proton.Drive.Sdk.Sync.Client.Authentication;

[Flags]
public enum MultiFactorAuthenticationMethods
{
    None = 0,
    Totp = 1,
    Fido2 = 2,
}
