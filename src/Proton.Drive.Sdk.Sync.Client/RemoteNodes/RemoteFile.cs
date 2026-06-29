using System.Collections.ObjectModel;
using System.Net.Mime;
using Proton.Cryptography.Pgp;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Client.RemoteNodes;

internal sealed record RemoteFile(
    Link Link,
    string Name,
    PgpSessionKey NameSessionKey,
    string? ParentPath,
    PgpPrivateKey PrivateKey,
    ReadOnlyMemory<byte> Passphrase,
    PgpSessionKey PassphraseSessionKey,
    PgpSessionKey ContentSessionKey,
    ExtendedAttributes? ExtendedAttributes)
    : RemoteNode(
        Link,
        ConvertRemoteNameToLocalName(Name, Link.MediaType),
        NameSessionKey,
        ParentPath,
        PrivateKey,
        Passphrase,
        PassphraseSessionKey,
        ExtendedAttributes)
{
    private static readonly ReadOnlyDictionary<string, string> MediaTypeToLocalExtensionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "application/vnd.proton.doc", ".protondoc" },
        { "application/vnd.proton.sheet", ".protonsheet" },
    }.AsReadOnly();

    public long SizeOnStorage => Link.FileProperties?.ActiveRevision?.Size ?? 0;
    public long? PlainSize => ExtendedAttributes?.Common?.Size;
    public string MediaType => Link.MediaType ?? MediaTypeNames.Application.Octet;
    public ReadOnlyMemory<byte> ContentKeyPacket => Link.FileProperties!.ContentKeyPacket;
    public RevisionHeader? ActiveRevision => Link.FileProperties!.ActiveRevision;

    public static string ConvertRemoteNameToLocalName(string remoteName, string? mediaType)
    {
        if (mediaType is null || !MediaTypeToLocalExtensionMap.TryGetValue(mediaType, out var extension) || remoteName.EndsWith(extension))
        {
            return remoteName;
        }

        var maxLengthWithoutExtension = Math.Min(FileNameFactory.MaxNameLength - extension.Length, remoteName.Length);
        return string.Concat(remoteName.AsSpan()[..maxLengthWithoutExtension], extension);
    }

    public static string ConvertLocalNameToRemoteName(string localName, string? mediaType)
    {
        if (mediaType is null || !MediaTypeToLocalExtensionMap.TryGetValue(mediaType, out var extension) || !localName.EndsWith(extension))
        {
            return localName;
        }

        return localName[..^extension.Length];
    }

    public override bool MatchesRemoteName(string remoteName)
    {
        return string.Equals(ConvertRemoteNameToLocalName(remoteName, Link.MediaType), Name, StringComparison.Ordinal);
    }

    public override NodeInfo<string> ToNodeInfo()
    {
        var result = base.ToNodeInfo()
            .WithRevisionId(ActiveRevision?.Id)
            .WithSize(PlainSize)
            .WithSizeOnStorage(SizeOnStorage)
            .WithSha1Digest(ExtendedAttributes?.Common?.Digests?.Sha1);

        if (Link.State == LinkState.Draft)
        {
            result = result.WithAttributes(result.Attributes | FileAttributes.Temporary);
        }

        return result;
    }

    protected override NodeInfo<string> CreateNodeInfo()
    {
        // Modification time is NOT used as File last write time, default value is used instead
        return NodeInfo<string>.File();
    }
}
