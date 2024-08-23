using Proton.Security.Cryptography.Abstractions;

namespace ProtonDrive.Client.Cryptography;

public sealed record AddressKey(string Id, PrivatePgpKey PrivateKey, bool IsAllowedForEncryption);
