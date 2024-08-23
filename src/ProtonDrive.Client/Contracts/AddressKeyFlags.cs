using System;

namespace ProtonDrive.Client.Contracts;

[Flags]
public enum AddressKeyFlags
{
    None = 0,
    IsAllowedForSignatureVerification,
    IsAllowedForEncryption,
    IsOwnedByExternalAddress,
}
