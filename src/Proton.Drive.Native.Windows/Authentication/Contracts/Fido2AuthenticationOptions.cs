namespace Proton.Drive.Native.Authentication.Contracts;

internal sealed class Fido2AuthenticationOptions
{
    public required PublicKeyCredentialRequestOptions PublicKey { get; set; }
}
