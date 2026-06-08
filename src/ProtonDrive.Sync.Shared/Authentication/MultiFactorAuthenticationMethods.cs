namespace ProtonDrive.App.Authentication;

[Flags]
public enum MultiFactorAuthenticationMethods
{
    None = 0,
    Totp = 1,
    Fido2 = 2,
}
