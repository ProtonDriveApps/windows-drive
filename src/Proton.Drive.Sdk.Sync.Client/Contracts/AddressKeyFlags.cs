namespace Proton.Drive.Sdk.Sync.Client.Contracts;

[Flags]
public enum AddressKeyFlags
{
    None = 0,
    IsAllowedForSignatureVerification,
    IsAllowedForEncryption,
    IsOwnedByExternalAddress,
}
