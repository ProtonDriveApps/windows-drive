using Proton.Cryptography.Pgp;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography;

public sealed record AddressKey(string Id, PgpPrivateKey PrivateKey, bool IsAllowedForEncryption, bool PrivateKeyIsUnlocked);
