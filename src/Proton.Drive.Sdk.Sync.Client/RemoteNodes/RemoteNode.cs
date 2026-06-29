using Proton.Cryptography.Pgp;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Client.RemoteNodes;

internal abstract record RemoteNode(
    Link Link,
    string Name,
    PgpSessionKey NameSessionKey,
    string? ParentPath,
    PgpPrivateKey PrivateKey,
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

    public DateTime? DeletionTime =>
        Link.DeletionTime is not null ? DateTimeOffset.FromUnixTimeSeconds(Link.DeletionTime.GetValueOrDefault()).UtcDateTime : default(DateTime?);

    public bool IsNodePassphraseSignedAnonymously => string.IsNullOrEmpty(Link.SignatureEmailAddress);

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
