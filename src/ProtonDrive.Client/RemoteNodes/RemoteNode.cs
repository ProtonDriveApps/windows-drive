using System;
using Proton.Security.Cryptography.Abstractions;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client.RemoteNodes;

internal abstract record RemoteNode(
    Link Link,
    string Name,
    PgpSessionKey NameSessionKey,
    string? ParentPath,
    PrivatePgpKey PrivateKey,
    ReadOnlyMemory<byte> Passphrase,
    PgpSessionKey PassphraseSessionKey,
    ExtendedAttributes? ExtendedAttributes) : IPrivateKeyHolder
{
    public string Id => Link.Id;
    public string? ParentId => Link.ParentId;
    public LinkState State => Link.State;
    public string? NameHash => Link.NameHash;
    public DateTime CreationTime => DateTimeOffset.FromUnixTimeSeconds(Link.CreationTime).UtcDateTime;

    public virtual DateTime ModificationTime =>
        ExtendedAttributes?.Common?.LastWriteTime
        ?? DateTimeOffset.FromUnixTimeSeconds(Link.ModificationTime).UtcDateTime;

    public DateTime? ExpirationTime =>
        Link.ExpirationTime is not null ? DateTimeOffset.FromUnixTimeSeconds(Link.ExpirationTime.GetValueOrDefault()).UtcDateTime : default(DateTime?);
    public DateTime? DeletionTime =>
        Link.DeletionTime is not null ? DateTimeOffset.FromUnixTimeSeconds(Link.DeletionTime.GetValueOrDefault()).UtcDateTime : default(DateTime?);

    protected Link Link { get; } = Link;

    public virtual bool MatchesRemoteName(string remoteName) => string.Equals(remoteName, Name, StringComparison.Ordinal);

    public virtual NodeInfo<string> ToNodeInfo()
    {
        return CreateNodeInfo()
            .WithId(Id)
            .WithParentId(ParentId)
            .WithName(Name)
            .WithLastWriteTimeUtc(ModificationTime);
    }

    protected abstract NodeInfo<string> CreateNodeInfo();
}
