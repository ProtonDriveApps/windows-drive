using System;
using System.Collections.Immutable;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Proton.Security.Cryptography;
using Proton.Security.Cryptography.Abstractions;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.Cryptography;
using ProtonDrive.Client.Shares;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Client.RemoteNodes;

internal sealed class RemoteNodeService : IRemoteNodeService
{
    private readonly IShareApiClient _shareApiClient;
    private readonly ILinkApiClient _linkApiClient;
    private readonly ICryptographyService _cryptographyService;
    private readonly IExtendedAttributesReader _extendedAttributesReader;
    private readonly ILogger<RemoteNodeService> _logger;

    private readonly AsyncCache<RemoteNodeCacheKey, RemoteNode> _remoteNodeCache;
    private readonly AsyncCache<ShareCacheKey, Share> _shareCache;

    public RemoteNodeService(
        IShareApiClient shareApiClient,
        ILinkApiClient linkApiClient,
        ICryptographyService cryptographyService,
        IExtendedAttributesReader extendedAttributesReader,
        IMemoryCache cache,
        ILogger<RemoteNodeService> logger)
    {
        _shareApiClient = shareApiClient;
        _linkApiClient = linkApiClient;
        _cryptographyService = cryptographyService;
        _extendedAttributesReader = extendedAttributesReader;
        _logger = logger;

        _remoteNodeCache = new AsyncCache<RemoteNodeCacheKey, RemoteNode>(cache);
        _shareCache = new AsyncCache<ShareCacheKey, Share>(cache);
    }

    public Task<RemoteNode> GetRemoteNodeAsync(string shareId, string linkId, CancellationToken cancellationToken)
    {
        return _remoteNodeCache.GetOrAddAsync(new RemoteNodeCacheKey(linkId), GetRemoteNodeForCacheAsync, cancellationToken);

        async Task<RemoteNode> GetRemoteNodeForCacheAsync()
        {
            var linkResponse = await _linkApiClient.GetLinkAsync(shareId, linkId, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
            var link = linkResponse.Link;
            if (link is null)
            {
                throw new ApiException(ResponseCode.InvalidValue, $"Could not get link with ID '{linkId}'");
            }

            return await GetRemoteNodeAsync(shareId, link, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<RemoteNode> GetRemoteNodeAsync(string shareId, Link link, CancellationToken cancellationToken)
    {
        // If the link to be decrypted is the shared one, we use the share for decryption instead of
        // looking for the parent.
        var parentKeyHolder = link.ParentId is not null && link.SharingDetails?.ShareId != shareId
            ? await GetRemoteNodeAsync(shareId, link.ParentId, cancellationToken).ConfigureAwait(false)
            : (IPrivateKeyHolder)await GetShareAsync(shareId, cancellationToken).ConfigureAwait(false);

        return await GetRemoteNodeAsync(parentKeyHolder, link, cancellationToken).ConfigureAwait(false);
    }

    public Task<RemoteNode> GetRemoteNodeAsync(IPrivateKeyHolder parent, Link link, CancellationToken cancellationToken)
    {
        return GetRemoteNodeAsync(parent, link, default, cancellationToken);
    }

    public async Task<RemoteNode> GetRemoteNodeFromHierarchyAsync(
        string rootShareId,
        IImmutableList<Link> linksHierarchy,
        CancellationToken cancellationToken)
    {
        var linkIndex = 0;
        var parentPathBuilder = new StringBuilder();

        // Cache all the ancestor nodes (without further API calls)
        while (linkIndex < linksHierarchy.Count - 1)
        {
            var link = linksHierarchy[linkIndex];

            var node = await _remoteNodeCache.GetOrAddAsync(
                    new RemoteNodeCacheKey(link.Id),
                    async () => await GetRemoteNodeAsync(rootShareId, link, cancellationToken).ConfigureAwait(false),
                    cancellationToken)
                .ConfigureAwait(false);

            var parentName = node.Name;

            var isRootFolder = linkIndex == 0;

            if (!isRootFolder)
            {
                parentPathBuilder.Append(Path.DirectorySeparatorChar).Append(parentName);
            }

            ++linkIndex;
        }

        var parentPath = parentPathBuilder.Length == 0 ? Path.DirectorySeparatorChar.ToString() : parentPathBuilder.ToString();

        return await GetRemoteNodeAsync(rootShareId, linksHierarchy[linkIndex], parentPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Share> GetShareAsync(string shareId, CancellationToken cancellationToken)
    {
        return await _shareCache.GetOrAddAsync(
                new ShareCacheKey(shareId),
                () => GetDecryptedShareForCacheAsync(shareId, cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<RemoteNode> GetRemoteNodeAsync(string shareId, Link link, string parentPath, CancellationToken cancellationToken)
    {
        var parentKeyHolder = link.ParentId is not null
            ? await GetRemoteNodeAsync(shareId, link.ParentId, cancellationToken).ConfigureAwait(false)
            : (IPrivateKeyHolder)await GetShareAsync(shareId, cancellationToken).ConfigureAwait(false);

        return await GetRemoteNodeAsync(parentKeyHolder, link, parentPath, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RemoteNode> GetRemoteNodeAsync(IPrivateKeyHolder parent, Link link, string? parentPath, CancellationToken cancellationToken)
    {
        var passphraseDecrypter = await _cryptographyService
            .CreateNodeNameAndKeyPassphraseDecrypterAsync(parent.PrivateKey, link.SignatureEmailAddress, cancellationToken)
            .ConfigureAwait(false);

        var (passphrase, passphraseSessionKey) = DecryptPassphrase(passphraseDecrypter, link.NodePassphrase, link.NodePassphraseSignature, "link", link.Id);

        var nodeKey = PrivatePgpKey.FromArmored(link.NodeKey, passphrase);

        // TODO: re-use the decrypter used to decrypt the passphrase
        var (name, nameSessionKey) = await DecryptNameAsync(link.Id, link.Name, parent.PrivateKey, link.NameSignatureEmailAddress, cancellationToken)
            .ConfigureAwait(false);

        var extendedAttributes = await _extendedAttributesReader.ReadAsync(link, nodeKey, cancellationToken).ConfigureAwait(false);

        switch (link.Type)
        {
            case LinkType.File:
                var contentSessionKey = await DecryptContentSessionKeyAsync(
                    link.Id,
                    link.FileProperties!.ContentKeyPacket,
                    link.FileProperties!.ContentKeyPacketSignature,
                    link.SignatureEmailAddress,
                    nodeKey,
                    cancellationToken).ConfigureAwait(false);

                return new RemoteFile(
                    link,
                    name,
                    nameSessionKey,
                    parentPath,
                    nodeKey,
                    passphrase,
                    passphraseSessionKey,
                    contentSessionKey,
                    extendedAttributes);

            case LinkType.Folder:
                var hashKey = DecryptHashKey(link.Id, link.FolderProperties?.NodeHashKey ?? string.Empty, nodeKey, nodeKey.PublicKey);

                return new RemoteFolder(
                    link,
                    name,
                    nameSessionKey,
                    parentPath,
                    nodeKey,
                    passphrase,
                    passphraseSessionKey,
                    hashKey.ToArray(),
                    extendedAttributes);

            default:
                throw new NotSupportedException($"Unknown link type '{link.Type}'");
        }
    }

    private async Task<Share> GetDecryptedShareForCacheAsync(string shareId, CancellationToken cancellationToken)
    {
        var share = await _shareApiClient.GetShareAsync(shareId, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        if (share.IsLocked || string.IsNullOrEmpty(share.AddressId))
        {
            // Address ID can be NULL when the share is locked
            throw new ApiException(ResponseCode.InvalidRequirements, "Share is locked");
        }

        // Share bootstrap memberships only contain entry of the current user
        var addressIds = share.Memberships.Select(m => m.AddressId);
        var decrypter = await _cryptographyService.CreateShareKeyPassphraseDecrypterAsync(addressIds, share.CreatorEmailAddress, cancellationToken).ConfigureAwait(false);
        var (passphrase, _) = DecryptPassphrase(decrypter, share.Passphrase, share.PassphraseSignature, "share", shareId);
        var privateKey = PrivatePgpKey.FromArmored(share.Key, passphrase);

        return new Share(share.LinkId, privateKey, share.AddressId);
    }

    private async Task<(string Name, PgpSessionKey SessionKey)> DecryptNameAsync(
        string linkId,
        string nameMessage,
        PrivatePgpKey decryptionKey,
        string? signatureEmailAddress,
        CancellationToken cancellationToken)
    {
        var decrypter = await _cryptographyService.CreateNodeNameAndKeyPassphraseDecrypterAsync(
            decryptionKey,
            signatureEmailAddress,
            cancellationToken).ConfigureAwait(false);

        try
        {
            var name = decrypter.DecryptAndVerifyText(nameMessage, out var verificationVerdict, out var sessionKey);

            LogIfSignatureIsInvalid(verificationVerdict, "link", linkId, "name");

            return (name, sessionKey);
        }
        catch (CryptographicException ex)
        {
            throw ex.ToDecryptionException("link", linkId, "name");
        }
    }

    private (ReadOnlyMemory<byte> Passphrase, PgpSessionKey SessionKey) DecryptPassphrase(
        IVerificationCapablePgpDecrypter decrypter,
        string passphraseMessage,
        string passphraseSignature,
        string objectType,
        string objectId)
    {
        try
        {
            var passphrase = decrypter.DecryptAndVerify(
                passphraseMessage,
                passphraseSignature,
                out var verificationVerdict,
                out var sessionKey);

            LogIfSignatureIsInvalid(verificationVerdict, objectType, objectId, "passphrase");

            return (passphrase, sessionKey);
        }
        catch (CryptographicException ex)
        {
            throw ex.ToDecryptionException(objectType, objectId, "passphrase");
        }
    }

    private ReadOnlyMemory<byte> DecryptHashKey(string linkId, string encryptedHashKey, PrivatePgpKey decryptionKey, PublicPgpKey verificationKey)
    {
        var hashKeyDecrypter = _cryptographyService.CreateHashKeyDecrypter(decryptionKey, verificationKey);

        try
        {
            // TODO: handle decryption failure or absence of hash key
            var hashKey = hashKeyDecrypter.DecryptAndVerify(encryptedHashKey, out var verificationVerdict);

            LogIfSignatureIsInvalid(verificationVerdict, "folder", linkId, "hash key");

            return hashKey;
        }
        catch (CryptographicException ex)
        {
            throw ex.ToDecryptionException("folder", linkId, "hash key");
        }
    }

    private async Task<PgpSessionKey> DecryptContentSessionKeyAsync(
        string linkId,
        ReadOnlyMemory<byte> contentKeyPacket,
        string? signature,
        string signatureAddress,
        PrivatePgpKey nodeKey,
        CancellationToken cancellationToken)
    {
        var contentSessionKeyDecrypter = await _cryptographyService
            .CreateFileContentsBlockKeyDecrypterAsync(nodeKey, signatureAddress, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var contentSessionKey = contentSessionKeyDecrypter.DecryptSessionKey(contentKeyPacket);

            var verificationVerdict = await VerifyContentSessionKeyAsync(
                contentSessionKeyDecrypter,
                contentSessionKey,
                contentKeyPacket,
                signature,
                cancellationToken).ConfigureAwait(false);

            LogIfSignatureIsInvalid(verificationVerdict, "file", linkId, "content key packet");

            return contentSessionKey;
        }
        catch (CryptographicException ex)
        {
            throw ex.ToDecryptionException("file", linkId, "content key packet");
        }
    }

    private static async Task<VerificationVerdict> VerifyContentSessionKeyAsync(
        IVerificationCapablePgpDecrypter decrypter,
        PgpSessionKey contentSessionKey,
        ReadOnlyMemory<byte> contentKeyPacket,
        string? signature,
        CancellationToken cancellationToken)
    {
        if (signature is null)
        {
            return VerificationVerdict.NoSignature;
        }

        var signatureSource = new PgpSignatureSource(new AsciiStream(signature), PgpArmoring.Ascii);

        await using (signatureSource.ConfigureAwait(false))
        {
            var verificationVerdict = await decrypter.VerifyAsync(contentSessionKey.Data, signatureSource, cancellationToken)
                .ConfigureAwait(false);

            // Legacy signature support
            if (verificationVerdict != VerificationVerdict.ValidSignature)
            {
                signatureSource.Stream.Seek(0, SeekOrigin.Begin);

                verificationVerdict = await decrypter.VerifyAsync(contentKeyPacket, signatureSource, cancellationToken)
                    .ConfigureAwait(false);
            }

            return verificationVerdict;
        }
    }

    private void LogIfSignatureIsInvalid(VerificationVerdict code, string objectType, string objectId, string attributeType)
    {
        if (code == VerificationVerdict.ValidSignature)
        {
            return;
        }

        _logger.LogWarning(
            "Signature problem on {AttributeType} of {ObjectType} with ID {Id}: {Code}",
            attributeType,
            objectType,
            objectId,
            code);
    }

    private record struct RemoteNodeCacheKey(string LinkId);

    private record struct ShareCacheKey(string LinkId);
}
