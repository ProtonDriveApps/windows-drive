using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Client.Cryptography;

internal interface IKeyPassphraseProvider
{
    bool ContainsAtLeastOnePassphrase { get; }
    Task CalculatePassphrasesAsync(SecureString password, CancellationToken cancellationToken);
    void ClearPassphrases();
    ReadOnlyMemory<byte> GetPassphrase(string keyId);
}
