namespace Proton.Drive.Sdk.Sync.Client.Cryptography;

internal record KeyPassphrases(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Passphrases);
