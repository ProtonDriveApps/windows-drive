using System.Security;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography;

internal interface IKeyPassphraseProvider
{
    bool ContainsAtLeastOnePassphrase { get; }
    Task CalculatePassphrasesAsync(SecureString password, CancellationToken cancellationToken);
    void ClearPassphrases();
    IReadOnlyDictionary<string, ReadOnlyMemory<byte>> GetPassphrases();
}
