using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Mime;
using Proton.Security.Cryptography.Abstractions;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client.RemoteNodes;

internal sealed record RemoteFile(
    Link Link,
    string Name,
    PgpSessionKey NameSessionKey,
    string? ParentPath,
    PrivatePgpKey PrivateKey,
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
    }.AsReadOnly();

    public long SizeOnStorage => Link.Size;
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
        var result = base.ToNodeInfo().WithRevisionId(ActiveRevision?.Id).WithSize(PlainSize).WithSizeOnStorage(SizeOnStorage);

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
