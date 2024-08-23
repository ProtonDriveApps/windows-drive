using System;
using Proton.Security.Cryptography.Abstractions;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client.RemoteNodes;

internal sealed record RemoteFolder(
        Link Link,
        string Name,
        PgpSessionKey NameSessionKey,
        string? ParentPath,
        PrivatePgpKey PrivateKey,
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
