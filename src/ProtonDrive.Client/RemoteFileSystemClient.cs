using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Proton.Security.Cryptography.Abstractions;
using ProtonDrive.BlockVerification;
using ProtonDrive.Client.Configuration;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.Cryptography;
using ProtonDrive.Client.FileUploading;
using ProtonDrive.Client.RemoteNodes;
using ProtonDrive.Client.Volumes;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Devices;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client;

public sealed class RemoteFileSystemClient : IFileSystemClient<string>
{
    private const int FolderChildListingPageSize = 150;
    private const int BufferSize = (1 << 22 /* 4M */) + (1 << 18 /* 256K */);
    private const int MaxNumberOfBuffers = 40;

    private readonly BlockingArrayMemoryPool<byte> _bufferPool = new(BufferSize, MaxNumberOfBuffers);

    private readonly FileExtensionContentTypeProvider _fileExtensionContentTypeProvider = new()
    {
        Mappings =
        {
            { ".apk", "application/vnd.android.package-archive" },
            { ".apng", "image/apng" },
            { ".arc", "application/x-freearc" },
            { ".avif", "image/avif" },
            { ".bzip2", "application/x-bzip2" },
            { ".cr3", "image/x-canon-cr3" },
            { ".epub", "application/epub+zip" },
            { ".flac", "audio/flac" },
            { ".gzip", "application/gzip" },
            { ".heic", "image/heic" },
            { ".heics", "image/heic-sequence" },
            { ".heif", "image/heif" },
            { ".heifs", "image/heif-sequence" },
            { ".keynote", "application/vnd.apple.keynote" },
            { ".mp1s", "video/mp1s" },
            { ".mp2p", "video/mp2p" },
            { ".mp2t", "video/mp2t" },
            { ".mp4a", "audio/mp4" },
            { ".numbers", "application/vnd.apple.numbers" },
            { ".odp", "application/vnd.oasis.opendocument.presentation" },
            { ".odt", "application/vnd.oasis.opendocument.text" },
            { ".opus", "audio/opus" },
            { ".pages", "application/vnd.apple.pages" },
            { ".qcp", "audio/qcelp" },
            { ".v3g2", "video/3gpp2" },
            { ".v3gp", "video/3gpp" },
            { ".x7zip", "application/x-7z-compressed" },
        },
    };

    private readonly DriveApiConfig _config;
    private readonly string _volumeId;
    private readonly string _shareId;
    private readonly string? _virtualParentId;
    private readonly string? _linkId;
    private readonly string? _linkName;
    private readonly IClientInstanceIdentityProvider _clientInstanceIdentityProvider;
    private readonly IRemoteNodeService _remoteNodeService;
    private readonly ILinkApiClient _linkApiClient;
    private readonly IFolderApiClient _folderApiClient;
    private readonly IFileApiClient _fileApiClient;
    private readonly IVolumeApiClient _volumeApiClient;
    private readonly ICryptographyService _cryptographyService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRevisionSealerFactory _revisionSealerFactory;
    private readonly IRevisionManifestCreator _revisionManifestCreator;
    private readonly IBlockVerifierFactory _blockVerifierFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Action<Exception> _reportBlockVerificationOrDecryptionFailure;

    internal RemoteFileSystemClient(
        DriveApiConfig config,
        FileSystemClientParameters fileSystemClientParameters,
        IClientInstanceIdentityProvider clientInstanceIdentityProvider,
        IRemoteNodeService remoteNodeService,
        ILinkApiClient linkApiClient,
        IFolderApiClient folderApiClient,
        IFileApiClient fileApiClient,
        IVolumeApiClient volumeApiClient,
        ICryptographyService cryptographyService,
        IHttpClientFactory httpClientFactory,
        IRevisionSealerFactory revisionSealerFactory,
        IRevisionManifestCreator revisionManifestCreator,
        IBlockVerifierFactory blockVerifierFactory,
        ILoggerFactory loggerFactory,
        Action<Exception> reportBlockVerificationOrDecryptionFailure)
    {
        _config = config;
        _volumeId = fileSystemClientParameters.VolumeId;
        _shareId = fileSystemClientParameters.ShareId;
        _virtualParentId = fileSystemClientParameters.VirtualParentId;
        _linkId = fileSystemClientParameters.LinkId;
        _linkName = fileSystemClientParameters.LinkName;
        _clientInstanceIdentityProvider = clientInstanceIdentityProvider;
        _remoteNodeService = remoteNodeService;
        _linkApiClient = linkApiClient;
        _folderApiClient = folderApiClient;
        _fileApiClient = fileApiClient;
        _volumeApiClient = volumeApiClient;
        _cryptographyService = cryptographyService;
        _httpClientFactory = httpClientFactory;
        _revisionSealerFactory = revisionSealerFactory;
        _revisionManifestCreator = revisionManifestCreator;
        _blockVerifierFactory = blockVerifierFactory;
        _loggerFactory = loggerFactory;
        _reportBlockVerificationOrDecryptionFailure = reportBlockVerificationOrDecryptionFailure;
    }

    private delegate Task<MultipleResponses<FolderChildrenDeletionResponse>> DeleteAsyncDelegate(
        string shareId,
        string linkId,
        MultipleNodeActionParameters parameters,
        CancellationToken cancellationToken);

    public static bool IncludeDraftNodesInEnumeration { get; set; }

    public void Connect(string syncRootPath, IFileHydrationDemandHandler<string> fileHydrationDemandHandler)
    {
        // Do nothing
    }

    public Task DisconnectAsync()
    {
        return Task.CompletedTask;
    }

    public async Task<NodeInfo<string>> GetInfo(NodeInfo<string> info, CancellationToken cancellationToken = default)
    {
        EnsureId(info.Id);

        cancellationToken.ThrowIfCancellationRequested();

        var node = await GetRemoteNodeAsync(info.Id, draftAllowed: true, cancellationToken).ConfigureAwait(false);

        return node.ToNodeInfo();
    }

    public async IAsyncEnumerable<NodeInfo<string>> Enumerate(NodeInfo<string> info, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Shared with me item
        if (_linkId is not null && _linkName is not null)
        {
            var node = await GetRemoteNodeAsync(_linkId, cancellationToken).ConfigureAwait(false);
            yield return node.ToNodeInfo().WithName(_linkName);
            yield break;
        }

        EnsureId(info.Id);

        cancellationToken.ThrowIfCancellationRequested();

        var remoteFolder = ToRemoteFolder(await GetRemoteNodeAsync(info.Id, cancellationToken).ConfigureAwait(false));

        var childListParameters = new FolderChildListParameters
        {
            PageIndex = 0,
            PageSize = FolderChildListingPageSize,
            ShowAll = IncludeDraftNodesInEnumeration,
        };

        FolderChildListResponse childListResponse;

        do
        {
            childListResponse = await GetFolderChildrenAsync(info.Id, childListParameters, cancellationToken).ConfigureAwait(false);

            foreach (var childLink in childListResponse.Links)
            {
                if (childLink.State != LinkState.Active && !(IncludeDraftNodesInEnumeration && childLink.State == LinkState.Draft))
                {
                    continue;
                }

                var childNode = await DecryptLinkAsync(childLink, remoteFolder, cancellationToken).ConfigureAwait(false);

                yield return childNode.ToNodeInfo();
            }

            ++childListParameters.PageIndex;
        }
        while (childListResponse.Links.Count >= FolderChildListingPageSize);
    }

    public async Task<NodeInfo<string>> CreateDirectory(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        EnsureParentId(info.ParentId);
        Ensure.NotNullOrEmpty(info.Name, nameof(info), nameof(info.Name));

        cancellationToken.ThrowIfCancellationRequested();

        var share = await GetShareAsync(cancellationToken).ConfigureAwait(false);

        var (parameters, nodeKey, _) =
            await CreateNodeCreationParametersAsync(info, () => new FolderCreationParameters(), share.RelevantMembershipAddressId, cancellationToken)
                .ConfigureAwait(false);

        var hashKey = _cryptographyService.GenerateHashKey();
        var hashKeyEncrypter = _cryptographyService.CreateHashKeyEncrypter(nodeKey.PublicKey, nodeKey);
        parameters.NodeHashKey = hashKeyEncrypter.EncryptHashKey(hashKey);

        var response = await CreateFolderAsync(parameters, cancellationToken).ConfigureAwait(false);

        return info.WithId(response.FolderId.Value);
    }

    public async Task<IRevisionCreationProcess<string>> CreateFile(
        NodeInfo<string> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        EnsureParentId(info.ParentId);
        Ensure.NotNullOrEmpty(info.Name, nameof(info), nameof(info.Name));

        cancellationToken.ThrowIfCancellationRequested();

        var share = await GetShareAsync(cancellationToken).ConfigureAwait(false);

        var (parameters, nodeKey, signatureAddress) = await CreateNodeCreationParametersAsync(
            info,
            () => new FileCreationParameters(),
            share.RelevantMembershipAddressId,
            cancellationToken).ConfigureAwait(false);

        parameters.MediaType = GetMediaType(info);

        var (keyPacket, sessionKey, sessionKeySignature) = _cryptographyService.GenerateFileContentKeyPacket(nodeKey.PublicKey, nodeKey, info.Name);
        parameters.ContentKeyPacket = keyPacket;
        parameters.ContentKeyPacketSignature = sessionKeySignature;
        parameters.ClientId = _clientInstanceIdentityProvider.GetClientInstanceId();

        var (response, fileDraftExists) = await CreateFileAsync(parameters, cancellationToken).ConfigureAwait(false);

        if (fileDraftExists)
        {
            (sessionKey, nodeKey) = await GetExistingKeysAsync(response.FileRevisionId.LinkId).ConfigureAwait(false);
        }

        var contentEncrypter = _cryptographyService.CreateFileBlockEncrypter(sessionKey, nodeKey.PublicKey, signatureAddress);

        var blockVerifier = await GetBlockVerifierAsync(response.FileRevisionId.LinkId, response.FileRevisionId.Value, nodeKey, cancellationToken)
            .ConfigureAwait(false);

        var stream = new RemoteFileWriteStream(
            _fileApiClient,
            _httpClientFactory,
            _cryptographyService,
            _bufferPool,
            _shareId,
            response.FileRevisionId.LinkId,
            response.FileRevisionId.Value,
            signatureAddress,
            contentEncrypter,
            thumbnailProvider,
            blockVerifier,
            _reportBlockVerificationOrDecryptionFailure,
            progressCallback);

        var extendedAttributesBuilder = new ExtendedAttributesBuilder(_cryptographyService, _loggerFactory.CreateLogger<ExtendedAttributesBuilder>())
        {
            NodeKey = nodeKey.PublicKey,
            LastWriteTime = info.LastWriteTimeUtc,
            Size = info.Size,
            SignatureAddress = signatureAddress,
        };

        var revisionSealer = _revisionSealerFactory.Create(
            _shareId,
            response.FileRevisionId.LinkId,
            response.FileRevisionId.Value,
            contentEncrypter,
            signatureAddress,
            extendedAttributesBuilder);

        var nodeInfoWithIds = info.WithId(response.FileRevisionId.LinkId).WithRevisionId(response.FileRevisionId.Value);

        return new RemoteRevisionCreationProcess(nodeInfoWithIds, stream, stream.UploadedBlocks, stream.BlockSize, revisionSealer);

        async Task<(PgpSessionKey ContentSessionKey, PrivatePgpKey NodeKey)> GetExistingKeysAsync(string linkId)
        {
            var existingNode = await GetRemoteNodeAsync(linkId, draftAllowed: true, cancellationToken).ConfigureAwait(false);

            if (existingNode is not RemoteFile existingFile)
            {
                throw new FileSystemClientException<string>($"Could not get session key for existing file with ID: {linkId}");
            }

            return (existingFile.ContentSessionKey, existingNode.PrivateKey);
        }
    }

    public async Task<IRevision> OpenFileForReading(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        EnsureId(info.Id);

        cancellationToken.ThrowIfCancellationRequested();

        var remoteFile = ToRemoteFile(await GetRemoteNodeAsync(info.Id, cancellationToken).ConfigureAwait(false));

        CheckMetadata(remoteFile, info);
        CheckLink(remoteFile, info);

        var stream = new SafeRemoteFileStream(
            new RemoteFileReadStream(
                _config,
                _fileApiClient,
                _volumeApiClient,
                _httpClientFactory,
                _cryptographyService,
                _revisionManifestCreator,
                _bufferPool,
                _volumeId,
                _shareId,
                remoteFile,
                _loggerFactory.CreateLogger<RemoteFileReadStream>(),
                _reportBlockVerificationOrDecryptionFailure),
            info.Id);

        return new RemoteFileRevision(stream, remoteFile.ModificationTime);
    }

    public async Task<IRevisionCreationProcess<string>> CreateRevision(
        NodeInfo<string> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        EnsureId(info.Id);

        cancellationToken.ThrowIfCancellationRequested();

        var share = await GetShareAsync(cancellationToken).ConfigureAwait(false);

        var remoteFile = ToRemoteFile(await GetRemoteNodeAsync(info.Id, cancellationToken).ConfigureAwait(false));
        CheckMetadata(remoteFile, info);
        CheckLink(remoteFile, info);

        var clientId = _clientInstanceIdentityProvider.GetClientInstanceId();
        var revisionId = await CreateRevisionAsync(info.Id, remoteFile.ActiveRevision?.Id, clientId, cancellationToken).ConfigureAwait(false);

        var nodeKey = remoteFile.PrivateKey;

        var (contentEncrypter, signatureAddress) = await _cryptographyService.CreateFileBlockEncrypterAsync(
            contentSessionKey: remoteFile.ContentSessionKey,
            signaturePublicKey: nodeKey.PublicKey,
            share.RelevantMembershipAddressId,
            cancellationToken).ConfigureAwait(false);

        var blockVerifier = await GetBlockVerifierAsync(remoteFile.Id, revisionId, nodeKey, cancellationToken)
            .ConfigureAwait(false);

        var stream = new RemoteFileWriteStream(
            _fileApiClient,
            _httpClientFactory,
            _cryptographyService,
            _bufferPool,
            _shareId,
            info.Id,
            revisionId,
            signatureAddress,
            contentEncrypter,
            thumbnailProvider,
            blockVerifier,
            _reportBlockVerificationOrDecryptionFailure,
            progressCallback);

        var extendedAttributesBuilder = new ExtendedAttributesBuilder(_cryptographyService, _loggerFactory.CreateLogger<ExtendedAttributesBuilder>())
        {
            NodeKey = nodeKey.PublicKey,
            LastWriteTime = lastWriteTime,
            Size = size,
            SignatureAddress = signatureAddress,
        };

        var revisionSealer = _revisionSealerFactory.Create(
            _shareId,
            info.Id,
            revisionId,
            contentEncrypter,
            signatureAddress,
            extendedAttributesBuilder);

        var nodeInfoWithIds = info.Copy()
            .WithRevisionId(revisionId)
            .WithParentId(_linkId is null ? remoteFile.ParentId : _virtualParentId)
            .WithSize(size)
            .WithLastWriteTimeUtc(lastWriteTime);

        return new RemoteRevisionCreationProcess(nodeInfoWithIds, stream, stream.UploadedBlocks, stream.BlockSize, revisionSealer);
    }

    public async Task Move(NodeInfo<string> info, NodeInfo<string> destinationInfo, CancellationToken cancellationToken)
    {
        EnsureId(info.Id);
        Ensure.IsFalse(
            string.IsNullOrEmpty(destinationInfo.Name) && string.IsNullOrEmpty(destinationInfo.ParentId),
            $"Both {nameof(destinationInfo)}.{nameof(destinationInfo.Name)} and {nameof(destinationInfo)}.{nameof(destinationInfo.ParentId)} cannot be null or empty.");

        cancellationToken.ThrowIfCancellationRequested();

        var share = await GetShareAsync(cancellationToken).ConfigureAwait(false);

        var nodeToMove = await GetRemoteNodeAsync(info.Id, cancellationToken).ConfigureAwait(false);

        // A volume root folder has no parent folder
        if (string.IsNullOrEmpty(nodeToMove.ParentId))
        {
            throw new InvalidOperationException($"Cannot move the volume root node with ID {nodeToMove.Id}");
        }

        CheckMetadata(nodeToMove, info);
        CheckLink(nodeToMove, info);

        var destinationName = !string.IsNullOrEmpty(destinationInfo.Name) ? destinationInfo.Name : info.Name;

        if (nodeToMove is RemoteFile remoteFile)
        {
            destinationName = RemoteFile.ConvertLocalNameToRemoteName(destinationName, remoteFile.MediaType);
        }

        var isRenameOnly = string.IsNullOrEmpty(destinationInfo.ParentId) || nodeToMove.ParentId == destinationInfo.ParentId;

        var destinationParentFolder = isRenameOnly
            ? await GetKeyHolderAsync(nodeToMove.ParentId, cancellationToken).ConfigureAwait(false)
            : ToRemoteFolder(await GetRemoteNodeAsync(destinationInfo.ParentId!, cancellationToken).ConfigureAwait(false));

        var (nameEncrypter, signatureAddress) = await _cryptographyService.CreateNodeNameAndKeyPassphraseEncrypterAsync(
            destinationParentFolder.PrivateKey.PublicKey,
            nodeToMove.NameSessionKey,
            share.RelevantMembershipAddressId,
            cancellationToken).ConfigureAwait(false);

        var name = nameEncrypter.EncryptNodeName(destinationName);
        var nameHash = _cryptographyService.HashNodeName(destinationParentFolder.HashKey, destinationName).ToHexString();

        if (!isRenameOnly)
        {
            var passphraseEncrypter = _cryptographyService.CreateNodeNameAndKeyPassphraseEncrypter(
                destinationParentFolder.PrivateKey.PublicKey,
                nodeToMove.PassphraseSessionKey,
                signatureAddress);

            var (encryptedPassphrase, _, _) = passphraseEncrypter.EncryptShareOrNodeKeyPassphrase(nodeToMove.Passphrase);

            var parameters = new MoveLinkParameters
            {
                ParentLinkId = destinationParentFolder.Id,
                NodePassphrase = encryptedPassphrase,
                Name = name,
                NameHash = nameHash,
                NameSignatureEmailAddress = signatureAddress.EmailAddress,
                OriginalNameHash = nodeToMove.NameHash,
            };

            await MoveNodeAsync(nodeToMove.Id, parameters, cancellationToken).ConfigureAwait(false);

            // MIME type is not updated when moving to another parent. If the file name has changed,
            // it might require to additionally use renaming (to the same name) to set a new MIME type.
        }

        if (isRenameOnly || destinationName != info.Name)
        {
            var mediaType = GetMediaType(destinationInfo);

            if (destinationInfo.ParentId == info.ParentId || mediaType != GetNodeMediaType(nodeToMove))
            {
                var parameters = new RenameLinkParameters
                {
                    Name = name,
                    NameHash = nameHash,
                    NameSignatureEmailAddress = signatureAddress.EmailAddress,
                    MediaType = mediaType,
                    OriginalNameHash = isRenameOnly ? nodeToMove.NameHash : nameHash,
                };

                try
                {
                    await RenameNodeAsync(nodeToMove.Id, parameters, cancellationToken).ConfigureAwait(false);
                }
                catch (FileSystemClientException) when (!isRenameOnly)
                {
                    // Ignore failure to update MIME type after successful move
                }
            }
        }

        static string GetNodeMediaType(RemoteNode node) => node is RemoteFile file ? file.MediaType : string.Empty;
    }

    public async Task Delete(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        EnsureId(info.Id);

        cancellationToken.ThrowIfCancellationRequested();

        var remoteNode = await GetRemoteNodeAsync(info.Id, draftAllowed: true, cancellationToken).ConfigureAwait(false);
        CheckMetadata(remoteNode, info);
        CheckLink(remoteNode, info);

        await DeleteAsync(remoteNode, _folderApiClient.MoveChildrenToTrashAsync, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeletePermanently(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        EnsureId(info.Id);

        cancellationToken.ThrowIfCancellationRequested();

        var remoteNode = await GetRemoteNodeAsync(info.Id, draftAllowed: true, cancellationToken).ConfigureAwait(false);
        CheckMetadata(remoteNode, info);
        CheckLink(remoteNode, info);

        await DeleteAsync(remoteNode, _folderApiClient.DeleteChildrenAsync, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteRevision(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        EnsureId(info.Id);
        Ensure.NotNullOrEmpty(info.RevisionId, nameof(info), nameof(info.RevisionId));

        cancellationToken.ThrowIfCancellationRequested();

        var remoteNode = await GetRemoteNodeAsync(info.Id, draftAllowed: true, cancellationToken).ConfigureAwait(false);
        CheckLink(remoteNode, info);

        await DeleteRevisionAsync(remoteNode, info.RevisionId, cancellationToken).ConfigureAwait(false);
    }

    public void SetInSyncState(NodeInfo<string> info)
    {
        // Do nothing
    }

    public Task HydrateFileAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        // Do nothing
        return Task.CompletedTask;
    }

    private static bool TryGetConflictingRevision<TResponse>(
        ApiException<TResponse> apiException,
        string clientId,
        [MaybeNullWhen(false)] out string linkId,
        [MaybeNullWhen(false)] out string draftRevisionId)
        where TResponse : IRevisionCreationConflictResponse
    {
        if (apiException is not { ResponseCode: ResponseCode.AlreadyExists, Content: not null })
        {
            linkId = null;
            draftRevisionId = null;
            return false;
        }

        var response = apiException.Content;

        if (response is not { Code: ResponseCode.AlreadyExists, Conflict: { } }
            || response.Conflict.ClientId != clientId
            || string.IsNullOrEmpty(response.Conflict?.DraftRevisionId))
        {
            linkId = null;
            draftRevisionId = null;
            return false;
        }

        linkId = response.Conflict.LinkId;
        draftRevisionId = response.Conflict.DraftRevisionId;
        return true;
    }

    private async Task DeleteAsync(RemoteNode remoteNode, DeleteAsyncDelegate deleteFunction, CancellationToken cancellationToken)
    {
        // A volume root folder has no parent folder
        if (string.IsNullOrEmpty(remoteNode.ParentId))
        {
            throw new InvalidOperationException($"Cannot delete the volume root node with ID {remoteNode.Id}");
        }

        var parameters = new MultipleNodeActionParameters(remoteNode.Id);

        try
        {
            var responses = await deleteFunction.Invoke(_shareId, remoteNode.ParentId, parameters, cancellationToken)
                .ThrowOnFailure().ConfigureAwait(false);

            var response = responses.Responses.SingleOrDefault(r => r.LinkId == remoteNode.Id)?.Response;
            if (response == null)
            {
                throw new ApiException(ResponseCode.InvalidValue, "API request returned empty response");
            }

            if (!response.Succeeded)
            {
                throw new ApiException(response.Code, response.Error ?? "API request failed");
            }
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, remoteNode.Id, includeObjectId: true, out var mappedException))
        {
            throw mappedException;
        }
    }

    private async Task DeleteRevisionAsync(RemoteNode remoteNode, string revisionId, CancellationToken cancellationToken)
    {
        var remoteFile = ToRemoteFile(remoteNode);

        try
        {
            var response = await _fileApiClient.DeleteRevisionAsync(_shareId, remoteFile.Id, revisionId, cancellationToken)
                .ThrowOnFailure().ConfigureAwait(false);

            if (response == null)
            {
                throw new ApiException(ResponseCode.InvalidValue, "API request returned empty response");
            }

            if (!response.Succeeded)
            {
                throw new ApiException(response.Code, response.Error ?? "API request failed");
            }
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, remoteFile.Id, includeObjectId: false, out var mappedException))
        {
            throw mappedException;
        }
    }

    private async Task<FolderChildListResponse> GetFolderChildrenAsync(
        string linkId,
        FolderChildListParameters childListParameters,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _folderApiClient.GetFolderChildrenAsync(_shareId, linkId, childListParameters, cancellationToken).ThrowOnFailure()
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, linkId, includeObjectId: true, out var mappedException))
        {
            throw mappedException;
        }
    }

    private async Task<FolderCreationResponse> CreateFolderAsync(NodeCreationParameters parameters, CancellationToken cancellationToken)
    {
        try
        {
            return await _folderApiClient.CreateFolderAsync(_shareId, parameters, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, id: parameters.ParentLinkId, includeObjectId: true, out var mappedException))
        {
            /* If something goes wrong, we assume there is a problem with the parent folder */

            throw mappedException;
        }
    }

    private async Task<(FileCreationResponse Response, bool FileDraftExists)> CreateFileAsync(
        FileCreationParameters parameters,
        CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                var result = await _fileApiClient.CreateFileAsync(_shareId, parameters, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
                return (result, false);
            }
            catch (ApiException<FileCreationResponse> ex) when (TryGetConflictingRevision(ex, parameters.ClientId, out var linkId, out var draftRevisionId))
            {
                var result = new FileCreationResponse
                {
                    FileRevisionId = new FileRevisionId
                    {
                        LinkId = linkId,
                        Value = draftRevisionId,
                    },
                };
                return (result, true);
            }
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, id: parameters.ParentLinkId, includeObjectId: true, out var mappedException))
        {
            /* If something goes wrong, we assume there is a problem with the parent folder */

            throw mappedException;
        }
    }

    private async Task<string> CreateRevisionAsync(string linkId, string? knownCurrentRevisionId, string clientId, CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                var parameters = new FileRevisionCreationParameters
                {
                    ClientId = _clientInstanceIdentityProvider.GetClientInstanceId(),
                    CurrentRevisionId = knownCurrentRevisionId,
                };
                var result = await _fileApiClient.CreateRevisionAsync(_shareId, linkId, parameters, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
                return result.RevisionId.Value;
            }
            catch (ApiException<RevisionCreationResponse> ex) when (TryGetConflictingRevision(ex, clientId, out _, out var draftRevisionId))
            {
                return draftRevisionId;
            }
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, linkId, includeObjectId: true, out var mappedException))
        {
            throw mappedException;
        }
    }

    private Task<RemoteNode> GetRemoteNodeAsync(string linkId, CancellationToken cancellationToken)
    {
        return GetRemoteNodeAsync(linkId, draftAllowed: false, cancellationToken);
    }

    private async Task<RemoteNode> GetRemoteNodeAsync(string linkId, bool draftAllowed, CancellationToken cancellationToken)
    {
        try
        {
            var linkResponse = await _linkApiClient.GetLinkAsync(_shareId, linkId, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

            var link = linkResponse.Link ?? throw new ApiException(ResponseCode.InvalidValue, "Link is not present in the API response");

            if (link.State is not LinkState.Active && (!draftAllowed || link.State is not LinkState.Draft))
            {
                throw new FileSystemClientException<string>($"File system object state is {link.State}", FileSystemErrorCode.ObjectNotFound, linkId);
            }

            if (link.Id == _linkId)
            {
                link = link with { ParentId = _virtualParentId };
            }

            return await DecryptLinkAsync(link, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, linkId, includeObjectId: true, out var mappedException))
        {
            throw mappedException;
        }
    }

    private async Task<Share> GetShareAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _remoteNodeService.GetShareAsync(_shareId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, default, includeObjectId: false, out var mappedException))
        {
            throw mappedException;
        }
    }

    private async Task MoveNodeAsync(string linkId, MoveLinkParameters parameters, CancellationToken cancellationToken)
    {
        try
        {
            await _linkApiClient.MoveLinkAsync(_shareId, linkId, parameters, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.ResponseCode is ResponseCode.InvalidRequirements or ResponseCode.TooManyChildren &&
                                      ExceptionMapping.TryMapException(ex, parameters.ParentLinkId, includeObjectId: true, out var mappedException))
        {
            /* A specific case when the limit of children is reached on the destination folder */

            throw mappedException;
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, linkId, includeObjectId: true, out var mappedException))
        {
            throw mappedException;
        }
    }

    private async Task RenameNodeAsync(string linkId, RenameLinkParameters parameters, CancellationToken cancellationToken)
    {
        try
        {
            await _linkApiClient.RenameLinkAsync(_shareId, linkId, parameters, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, linkId, includeObjectId: true, out var mappedException))
        {
            throw mappedException;
        }
    }

    private async Task<RemoteNode> DecryptLinkAsync(Link link, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(link.ParentId))
        {
            // A volume root folder has no parent folder
            try
            {
                return await _remoteNodeService.GetRemoteNodeAsync(_shareId, link, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ExceptionMapping.TryMapException(ex, link.Id, includeObjectId: false, out var mappedException))
            {
                throw mappedException;
            }
        }

        var keyHolder = link.Id != _linkId
            ? (IPrivateKeyHolder)await GetKeyHolderAsync(link.ParentId, cancellationToken).ConfigureAwait(false)
            : await GetShareAsync(cancellationToken).ConfigureAwait(false);

        return await DecryptLinkAsync(link, keyHolder, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RemoteNode> DecryptLinkAsync(Link link, IPrivateKeyHolder parent, CancellationToken cancellationToken)
    {
        try
        {
            return await _remoteNodeService.GetRemoteNodeAsync(parent, link, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, link.Id, includeObjectId: true, out var mappedException))
        {
            throw mappedException;
        }
    }

    private async Task<RemoteFolder> GetKeyHolderAsync(string parentLinkId, CancellationToken cancellationToken)
    {
        RemoteNode parentNode;

        try
        {
            parentNode = await _remoteNodeService.GetRemoteNodeAsync(_shareId, parentLinkId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, parentLinkId, includeObjectId: false, out var mappedException))
        {
            throw mappedException;
        }

        return ToRemoteFolder(parentNode);
    }

    private async Task<IBlockVerifier> GetBlockVerifierAsync(string linkId, string revisionId, PrivatePgpKey nodeKey, CancellationToken cancellationToken)
    {
        try
        {
            return await _blockVerifierFactory.CreateAsync(_shareId, linkId, revisionId, nodeKey, cancellationToken)
                .WithApiFailureMapping()
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, linkId, includeObjectId: true, out var mappedException))
        {
            throw mappedException;
        }
    }

    private RemoteFolder ToRemoteFolder(RemoteNode node)
    {
        if (node is RemoteFolder folder)
        {
            return folder;
        }

        throw new FileSystemClientException<string>(
            $"The link with ID={node.Id} is not a folder",
            FileSystemErrorCode.MetadataMismatch,
            node.Id);
    }

    private RemoteFile ToRemoteFile(RemoteNode node)
    {
        if (node is RemoteFile file)
        {
            return file;
        }

        throw new FileSystemClientException<string>(
            $"The link with ID={node.Id} is not a file",
            FileSystemErrorCode.MetadataMismatch,
            node.Id);
    }

    private void CheckMetadata(RemoteNode remoteNode, NodeInfo<string> info)
    {
        if (!IsFileRevisionExpected(remoteNode, info))
        {
            throw new FileSystemClientException<string>(
                $"Client-side optimistic locking failure: File revision has diverged, expected {info.RevisionId} but found {(remoteNode as RemoteFile)?.ActiveRevision?.Id}",
                FileSystemErrorCode.MetadataMismatch,
                info.Id);
        }

        if (!IsModificationTimeExpected(remoteNode, info))
        {
            throw new FileSystemClientException<string>(
                "Client-side optimistic locking failure: Last write time has diverged",
                FileSystemErrorCode.MetadataMismatch,
                info.Id);
        }

        if (!IsFileSizeExpected(remoteNode, info))
        {
            throw new FileSystemClientException<string>(
                "Client-side optimistic locking failure: File size has diverged",
                FileSystemErrorCode.MetadataMismatch,
                info.Id);
        }
    }

    private bool IsFileRevisionExpected(RemoteNode remoteNode, NodeInfo<string> info)
    {
        if (remoteNode is not RemoteFile remoteFile || info.RevisionId == null)
        {
            return true;
        }

        return remoteFile.ActiveRevision?.Id == info.RevisionId;
    }

    private bool IsModificationTimeExpected(RemoteNode remoteNode, NodeInfo<string> info)
    {
        // Modification time is used as Folder last write time.
        // Modification time for files is not checked in favor of checking Revision ID.
        if (remoteNode is not RemoteFolder _ || info.LastWriteTimeUtc == default)
        {
            return true;
        }

        return remoteNode.ModificationTime == info.LastWriteTimeUtc;
    }

    private bool IsFileSizeExpected(RemoteNode remoteNode, NodeInfo<string> info)
    {
        if (remoteNode is not RemoteFile remoteFile || info.Size < 0)
        {
            return true;
        }

        // The size on storage value is always present, but the plain size can be missing.
        // In previous versions, the size on storage was stored on the adapter and used for metadata check.
        // For compatibility, we compare both the plain file size and size on storage.
        return remoteFile.PlainSize == info.Size || remoteFile.SizeOnStorage == info.Size;
    }

    private void CheckLink(RemoteNode remoteNode, NodeInfo<string> info)
    {
        if (!string.IsNullOrEmpty(info.ParentId) && remoteNode.ParentId != info.ParentId)
        {
            throw new FileSystemClientException<string>(
                $"Client-side optimistic locking failure: Parent link has diverged, expected {info.ParentId} but found {remoteNode.ParentId}",
                FileSystemErrorCode.MetadataMismatch,
                info.Id);
        }

        if (_linkId is not null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(info.Name) && !remoteNode.MatchesRemoteName(info.Name))
        {
            throw new FileSystemClientException<string>(
                "Client-side optimistic locking failure: Node name has diverged",
                FileSystemErrorCode.MetadataMismatch,
                info.Id);
        }
    }

    private async Task<(T Parameters, PrivatePgpKey NodeKey, Address SignatureAddress)> CreateNodeCreationParametersAsync<T>(
        NodeInfo<string> info,
        Func<T> factory,
        string signatureAddressId,
        CancellationToken cancellationToken)
        where T : NodeCreationParameters
    {
        var parentFolder = ToRemoteFolder(await GetRemoteNodeAsync(info.ParentId!, cancellationToken).ConfigureAwait(false));

        try
        {
            var (parentEncrypter, signatureAddress) = await _cryptographyService.CreateNodeNameAndKeyPassphraseEncrypterAsync(
                parentFolder.PrivateKey.PublicKey,
                signatureAddressId,
                cancellationToken).ConfigureAwait(false);

            var passphrase = _cryptographyService.GeneratePassphrase();
            var nodeKey = _cryptographyService.GenerateShareOrNodeKey(passphrase);
            var (encryptedPassphrase, passphraseSignature, _) = parentEncrypter.EncryptShareOrNodeKeyPassphrase(passphrase);

            var parameters = factory.Invoke();

            parameters.Name = parentEncrypter.EncryptNodeName(info.Name);
            parameters.ParentLinkId = info.ParentId;
            parameters.NodeKey = nodeKey.ToString();
            parameters.NodePassphrase = encryptedPassphrase;
            parameters.NodePassphraseSignature = passphraseSignature;
            parameters.SignatureEmailAddress = signatureAddress.EmailAddress;
            parameters.NameHash = _cryptographyService.HashNodeName(parentFolder.HashKey, info.Name).ToHexString();

            return (parameters, nodeKey, signatureAddress);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, info.ParentId, includeObjectId: false, out var mappedException))
        {
            throw mappedException;
        }
    }

    private string? GetMediaType(NodeInfo<string> nodeInfo)
    {
        if (nodeInfo.IsDirectory())
        {
            return null;
        }

        return _fileExtensionContentTypeProvider.TryGetContentType(nodeInfo.Name, out var contentType)
            ? contentType
            : MediaTypeNames.Application.Octet;
    }

    private void EnsureId([NotNull] string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new FileSystemClientException<string>(
                "Node Id value is not specified",
                FileSystemErrorCode.PathBasedAccessNotSupported,
                objectId: default);
        }
    }

    private void EnsureParentId([NotNull] string? parentId)
    {
        if (string.IsNullOrEmpty(parentId))
        {
            throw new FileSystemClientException<string>(
                "Parent node Id value is not specified",
                FileSystemErrorCode.PathBasedAccessNotSupported,
                objectId: default);
        }
    }
}
