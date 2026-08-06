using Proton.Cryptography.Pgp;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Client.RemoteNodes;

internal sealed record RemoteFolder(
        Link Link,
        string Name,
        PgpSessionKey NameSessionKey,
        string? ParentPath,
        PgpPrivateKey PrivateKey,
        ReadOnlyMemory<byte> Passphrase,
        PgpSessionKey PassphraseSessionKey,
        byte[] HashKey,
        ExtendedAttributes? ExtendedAttributes)
    : RemoteNode(Link, Name, NameSessionKey, ParentPath, PrivateKey, Passphrase, PassphraseSessionKey, ExtendedAttributes)
{
    protected override NodeInfo<string> CreateNodeInfo()
    {
        // Modification time is used as Folder last write time
        return NodeInfo<string>.Directory().WithLastWriteTimeUtc(ModificationTime);
    }
}
