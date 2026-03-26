using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Proton.Cryptography.Pgp;
using ProtonDrive.Client.Albums.Contracts;
using ProtonDrive.Client.BlockVerification;
using ProtonDrive.Client.Configuration;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.Cryptography;
using ProtonDrive.Client.FileUploading;
using ProtonDrive.Client.MediaTypes;
using ProtonDrive.Client.RemoteNodes;
using ProtonDrive.Client.Volumes;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Devices;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Features;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;
using FileCreationParameters = ProtonDrive.Client.Contracts.FileCreationParameters;

namespace ProtonDrive.Client;

internal sealed class RemoteFileSystemClient : RemoteFileSystemClientBase, IFileSystemClient<string>
{
    private const int FolderChildListingPageSize = 150;

    private readonly BlockingArrayMemoryPool<byte> _bufferPool;

    private readonly DriveApiConfig _config;
    private readonly string _volumeId;
    private readonly string _shareId;
    private readonly string? _virtualParentId;
    private readonly string? _linkId;
    private readonly string? _linkName;
    private readonly bool _isPhotoClient;
    private readonly IClientInstanceIdentityProvider _clientInstanceIdentityProvider;
    private readonly ILinkApiClient _linkApiClient;
    private readonly IFolderApiClient _folderApiClient;
    private readonly IFileApiClient _fileApiClient;
    private readonly IPhotoApiClient _photoApiClient;
    private readonly IVolumeApiClient _volumeApiClient;
    private readonly ICryptographyService _cryptographyService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRevisionSealerFactory _revisionSealerFactory;
    private readonly IRevisionManifestCreator _revisionManifestCreator;
    private readonly IBlockVerifierFactory _blockVerifierFactory;
    private readonly IFeatureFlagProvider _featureFlagProvider;
    private readonly Action<Exception> _reportBlockVerificationOrDecryptionFailure;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<RemoteFileSystemClient> _logger;

    internal RemoteFileSystemClient(
        DriveApiConfig config,
        FileSystemClientParameters fileSystemClientParameters,
        IFileContentTypeProvider fileContentTypeProvider,
        IClientInstanceIdentityProvider clientInstanceIdentityProvider,
        IRemoteNodeService remoteNodeService,
        ILinkApiClient linkApiClient,
        IFolderApiClient folderApiClient,
        IFileApiClient fileApiClient,
        IPhotoApiClient photoApiClient,
        IVolumeApiClient volumeApiClient,
        ICryptographyService cryptographyService,
        IHttpClientFactory httpClientFactory,
        IRevisionSealerFactory revisionSealerFactory,
        IRevisionManifestCreator revisionManifestCreator,
        IBlockVerifierFactory blockVerifierFactory,
        IFeatureFlagProvider featureFlagProvider,
        Action<Exception> reportBlockVerificationOrDecryptionFailure,
        ILoggerFactory loggerFactory)
        : base(fileSystemClientParameters, linkApiClient, remoteNodeService, fileContentTypeProvider)
    {
        _config = config;
        _volumeId = fileSystemClientParameters.VolumeId;
        _shareId = fileSystemClientParameters.ShareId;
        _virtualParentId = fileSystemClientParameters.VirtualParentId;
        _linkId = fileSystemClientParameters.LinkId;
        _linkName = fileSystemClientParameters.LinkName;
        _isPhotoClient = fileSystemClientParameters.IsPhotoClient;
        _clientInstanceIdentityProvider = clientInstanceIdentityProvider;
        _linkApiClient = linkApiClient;
        _folderApiClient = folderApiClient;
        _fileApiClient = fileApiClient;
        _photoApiClient = photoApiClient;
        _volumeApiClient = volumeApiClient;
        _cryptographyService = cryptographyService;
        _httpClientFactory = httpClientFactory;
        _revisionSealerFactory = revisionSealerFactory;
        _revisionManifestCreator = revisionManifestCreator;
        _blockVerifierFactory = blockVerifierFactory;
        _featureFlagProvider = featureFlagProvider;
        _reportBlockVerificationOrDecryptionFailure = reportBlockVerificationOrDecryptionFailure;
        _loggerFactory = loggerFactory;

        _logger = _loggerFactory.CreateLogger<RemoteFileSystemClient>();

        _bufferPool = GetBufferPool();
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

    public async Task<NodeInfo<string>> GetInfoAsync(NodeInfo<string> info, CancellationToken cancellationToken = default)
    {
        EnsureId(info.Id);

        cancellationToken.ThrowIfCancellationRequested();

        var node = await GetRemoteNodeAsync(info.Id, draftAllowed: true, cancellationToken).ConfigureAwait(false);

        return node.ToNodeInfo();
    }

    public async IAsyncEnumerable<NodeInfo<string>> EnumerateAsync(NodeInfo<string> info, [EnumeratorCancellation] CancellationToken cancellationToken)
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

    public async Task<NodeInfo<string>> CreateDirectoryAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        EnsureParentId(info.ParentId);
        Ensure.NotNullOrEmpty(info.Name, nameof(info), nameof(info.Name));

        cancellationToken.ThrowIfCancellationRequested();

        var share = await GetShareAsync(cancellationToken).ConfigureAwait(false);

        var (parameters, nodeKey, _) =
            await CreateNodeCreationParametersAsync(info, () => new FolderCreationParameters(), share.RelevantMembershipAddressId, cancellationToken)
                .ConfigureAwait(false);

        var hashKey = _cryptographyService.GenerateHashKey();
        var hashKeyEncrypter = _cryptographyService.CreateHashKeyEncrypter(nodeKey.ToPublic(), nodeKey);
        parameters.NodeHashKey = hashKeyEncrypter.EncryptHashKey(hashKey);

        if (_isPhotoClient)
        {
            var albumCreationParameters = new AlbumCreationParameters
            {
                LinkCreationParameters = AlbumLinkCreationParameters.FromFolderCreationParameters(parameters),
            };

            var albumResponse = await CreateAlbumAsync(albumCreationParameters, cancellationToken).ConfigureAwait(false);

            return info.WithId(albumResponse.Album.LinkId.Value);
        }

        var response = await CreateFolderAsync(parameters, cancellationToken).ConfigureAwait(false);

        return info.WithId(response.FolderId.Value);
    }

    public async Task<IDestinationRevision<string>> CreateFileAsync(
        NodeInfo<string> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
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

        var (keyPacket, sessionKey, sessionKeySignature) = _cryptographyService.GenerateFileContentKeyPacket(nodeKey.ToPublic(), nodeKey, info.Name);
        parameters.ContentKeyPacket = keyPacket;
        parameters.ContentKeyPacketSignature = sessionKeySignature;
        parameters.ClientId = _clientInstanceIdentityProvider.GetClientInstanceId();

        var (response, fileDraftExists) = await CreateFileAsync(parameters, cancellationToken).ConfigureAwait(false);

        if (fileDraftExists)
        {
            (sessionKey, nodeKey) = await GetExistingKeysAsync(response.FileRevisionId.LinkId).ConfigureAwait(false);
        }

        var contentEncrypter = _cryptographyService.CreateFileBlockEncrypter(sessionKey, nodeKey.ToPublic(), signatureAddress);

        var blockVerifier = await GetBlockVerifierAsync(response.FileRevisionId.LinkId, response.FileRevisionId.Value, nodeKey, cancellationToken)
            .ConfigureAwait(false);

        var fileIdentity = new FileIdentity(_volumeId, _shareId, response.FileRevisionId.LinkId, response.FileRevisionId.Value);

        var stream = new RemoteFileWriteStream(
            _fileApiClient,
            _httpClientFactory,
            _cryptographyService,
            _bufferPool,
            fileIdentity,
            signatureAddress,
            contentEncrypter,
            thumbnailProvider,
            blockVerifier,
            _reportBlockVerificationOrDecryptionFailure,
            progressCallback);

        var extendedAttributesBuilder = new ExtendedAttributesBuilder(
            _cryptographyService,
            fileMetadataProvider,
            _loggerFactory.CreateLogger<ExtendedAttributesBuilder>())
        {
            NodeKey = nodeKey.ToPublic(),
            LastWriteTime = info.LastWriteTimeUtc,
            Size = info.Size,
            SignatureAddress = signatureAddress,
        };

        IRevisionSealer revisionSealer;

        var nodeInfoWithIds = info.WithId(response.FileRevisionId.LinkId).WithRevisionId(response.FileRevisionId.Value);

        var checksumVerificationEnabled = await _featureFlagProvider.UploadChecksumVerificationIsEnabledAsync(cancellationToken).ConfigureAwait(false);

        if (_isPhotoClient)
        {
            revisionSealer = _revisionSealerFactory.CreatePhotoSealer(
                new RevisionSealerParameters(_shareId, response.FileRevisionId.LinkId, response.FileRevisionId.Value, info.ParentId),
                contentEncrypter,
                signatureAddress,
                extendedAttributesBuilder,
                fileMetadataProvider);

            return new RemotePhotoRevisionCreationProcess(
                nodeInfoWithIds,
                checksumVerificationEnabled,
                stream,
                stream.UploadedBlocks,
                stream.BlockSize,
                fileMetadataProvider.CreationTimeUtc,
                fileMetadataProvider.LastWriteTimeUtc,
                revisionSealer,
                _loggerFactory.CreateLogger<RemotePhotoRevisionCreationProcess>());
        }

        revisionSealer = _revisionSealerFactory.CreateRegularSealer(
                new RevisionSealerParameters(_shareId, response.FileRevisionId.LinkId, response.FileRevisionId.Value, info.ParentId),
                contentEncrypter,
                signatureAddress,
                extendedAttributesBuilder);

        return new RemoteRevisionCreationProcess(
            nodeInfoWithIds,
            checksumVerificationEnabled,
            stream,
            stream.UploadedBlocks,
            stream.BlockSize,
            revisionSealer);

        async Task<(PgpSessionKey ContentSessionKey, PgpPrivateKey NodeKey)> GetExistingKeysAsync(string linkId)
        {
            var existingNode = await GetRemoteNodeAsync(linkId, draftAllowed: true, cancellationToken).ConfigureAwait(false);

            if (existingNode is not RemoteFile existingFile)
            {
                throw new FileSystemClientException<string>($"Could not get session key for existing file with ID: {linkId}");
            }

            return (existingFile.ContentSessionKey, existingNode.PrivateKey);
        }
    }

    public async Task<ISourceRevision> OpenFileForReadingAsync(NodeInfo<string> info, CancellationToken cancellationToken)
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

        return new RemoteFileRevision(
            stream,
            remoteFile.CreationTime,
            remoteFile.ModificationTime,
            remoteFile.ExtendedAttributes,
            remoteFile.ActiveRevision?.ChecksumVerified);
    }

    public async Task<IDestinationRevision<string>> CreateRevisionAsync(
        NodeInfo<string> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
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
            signaturePublicKey: nodeKey.ToPublic(),
            share.RelevantMembershipAddressId,
            cancellationToken).ConfigureAwait(false);

        var blockVerifier = await GetBlockVerifierAsync(remoteFile.Id, revisionId, nodeKey, cancellationToken)
            .ConfigureAwait(false);

        var fileIdentity = new FileIdentity(_volumeId, _shareId, info.Id, revisionId);

        var stream = new RemoteFileWriteStream(
            _fileApiClient,
            _httpClientFactory,
            _cryptographyService,
            _bufferPool,
            fileIdentity,
            signatureAddress,
            contentEncrypter,
            thumbnailProvider,
            blockVerifier,
            _reportBlockVerificationOrDecryptionFailure,
            progressCallback);

        var extendedAttributesBuilder = new ExtendedAttributesBuilder(
            _cryptographyService,
            fileMetadataProvider,
            _loggerFactory.CreateLogger<ExtendedAttributesBuilder>())
        {
            NodeKey = nodeKey.ToPublic(),
            LastWriteTime = lastWriteTime,
            Size = size,
            SignatureAddress = signatureAddress,
        };

        // Photos do not support revisions
        var revisionSealer = _revisionSealerFactory.CreateRegularSealer(
            new RevisionSealerParameters(_shareId, info.Id, revisionId),
            contentEncrypter,
            signatureAddress,
            extendedAttributesBuilder);

        var nodeInfoWithIds = info.Copy()
            .WithRevisionId(revisionId)
            .WithParentId(_linkId is null ? remoteFile.ParentId : _virtualParentId)
            .WithSize(size)
            .WithLastWriteTimeUtc(lastWriteTime);

        var checksumVerificationEnabled = await _featureFlagProvider.UploadChecksumVerificationIsEnabledAsync(cancellationToken).ConfigureAwait(false);

        return new RemoteRevisionCreationProcess(
            nodeInfoWithIds,
            checksumVerificationEnabled,
            stream,
            stream.UploadedBlocks,
            stream.BlockSize,
            revisionSealer);
    }

    public async Task MoveAsync(IReadOnlyList<NodeInfo<string>> sourceNodes, NodeInfo<string> destinationInfo, CancellationToken cancellationToken)
    {
        if (!_isPhotoClient)
        {
            throw new NotSupportedException();
        }

        Ensure.IsFalse(
            string.IsNullOrEmpty(destinationInfo.ParentId),
            $"{nameof(destinationInfo)}.{nameof(destinationInfo.ParentId)} cannot be null or empty.");

        var photosToAddParameters = new List<PhotoToAddParameter>(sourceNodes.Count);

        var parameters = new FetchLinksMetadataParameters
        {
            LinkIds = sourceNodes.Select(
                x => x.Id ?? throw new FileSystemClientException<string>(
                    "Node Id value is not specified",
                    FileSystemErrorCode.PathBasedAccessNotSupported,
                    objectId: null)).ToList(),
        };

        var linkListResponse = await _linkApiClient.GetLinksAsync(_shareId, parameters, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        // The response lacks nodes that do not exist or cannot be accessed
        var numberOfMissingNodes = sourceNodes.Count - linkListResponse.Links.Count;

        if (numberOfMissingNodes != 0)
        {
            _logger.LogWarning("Failed to retrieve {NumberOfMissingNodes} link(s)", numberOfMissingNodes);
            throw new FileSystemClientException<string>($"Unable to retrieve {numberOfMissingNodes} link(s)");
        }

        var linksById = linkListResponse.Links.ToDictionary(x => x.Id);

        var destinationParentFolder = ToRemoteFolder(await GetRemoteNodeAsync(destinationInfo.ParentId, cancellationToken).ConfigureAwait(false));

        foreach (var node in sourceNodes)
        {
            _logger.LogDebug("Adding file with ID {FileId}", node.Id);

            EnsureId(node.Id);

            cancellationToken.ThrowIfCancellationRequested();

            var share = await GetShareAsync(cancellationToken).ConfigureAwait(false);

            var nodeToAdd = await GetRemoteNodeAsync(linksById[node.Id], draftAllowed: false, cancellationToken).ConfigureAwait(false);

            CheckMetadata(nodeToAdd, node);
            CheckLink(nodeToAdd, node);

            var destinationName = node.Name;

            var (nameEncrypter, signatureAddress) = await _cryptographyService.CreateNodeNameAndKeyPassphraseEncrypterAsync(
                destinationParentFolder.PrivateKey.ToPublic(),
                nodeToAdd.NameSessionKey,
                share.RelevantMembershipAddressId,
                cancellationToken).ConfigureAwait(false);

            var name = nameEncrypter.EncryptNodeName(destinationName);
            var nameHash = _cryptographyService.HashNodeNameHex(destinationParentFolder.HashKey, destinationName);

            var passphraseEncrypter = _cryptographyService.CreateNodeNameAndKeyPassphraseEncrypter(
                destinationParentFolder.PrivateKey.ToPublic(),
                nodeToAdd.PassphraseSessionKey,
                signatureAddress);

            var (encryptedPassphrase, _, _) = passphraseEncrypter.EncryptShareOrNodeKeyPassphrase(nodeToAdd.Passphrase);

            Ensure.NotNullOrEmpty(node.Sha1Digest, nameof(node.Sha1Digest));

            photosToAddParameters.Add(new PhotoToAddParameter
            {
                Name = name,
                LinkId = node.Id,
                NameHash = nameHash,
                NameSignatureEmailAddress = signatureAddress.EmailAddress,
                NodePassphrase = encryptedPassphrase,
                ContentHash = _cryptographyService.HashContentDigestHex(destinationParentFolder.HashKey, node.Sha1Digest),
            });
        }

        await AddToAlbumAsync(albumLinkId: destinationInfo.ParentId, new PhotoToAddListParameters { Photos = photosToAddParameters }, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task MoveAsync(NodeInfo<string> info, NodeInfo<string> destinationInfo, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Moving file with ID {FileID} to {DestinationName}", info.Id, destinationInfo.Name);

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
            destinationParentFolder.PrivateKey.ToPublic(),
            nodeToMove.NameSessionKey,
            share.RelevantMembershipAddressId,
            cancellationToken).ConfigureAwait(false);

        var name = nameEncrypter.EncryptNodeName(destinationName);
        var nameHash = _cryptographyService.HashNodeNameHex(destinationParentFolder.HashKey, destinationName);

        if (!isRenameOnly)
        {
            var passphraseEncrypter = _cryptographyService.CreateNodeNameAndKeyPassphraseEncrypter(
                destinationParentFolder.PrivateKey.ToPublic(),
                nodeToMove.PassphraseSessionKey,
                signatureAddress);

            var (encryptedPassphrase, signature, _) = passphraseEncrypter.EncryptShareOrNodeKeyPassphrase(nodeToMove.Passphrase);

            var isSigningNodePassphraseRequired = nodeToMove.IsNodePassphraseSignedAnonymously;

            if (_isPhotoClient)
            {
                var albumLinkId = destinationInfo.ParentId;
                EnsureId(albumLinkId);
                Ensure.NotNullOrEmpty(info.Sha1Digest, nameof(info.Sha1Digest));

                var photosParameters = new PhotoToAddListParameters
                {
                    Photos =
                    [
                        new PhotoToAddParameter
                        {
                            Name = name,
                            LinkId = info.Id,
                            NameHash = nameHash,
                            NameSignatureEmailAddress = signatureAddress.EmailAddress,
                            NodePassphrase = encryptedPassphrase,
                            ContentHash = _cryptographyService.HashContentDigestHex(destinationParentFolder.HashKey, info.Sha1Digest),
                        },
                    ],
                };

                await AddToAlbumAsync(albumLinkId, photosParameters, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var parameters = new MoveLinkParameters
                {
                    ParentLinkId = destinationParentFolder.Id,
                    NodePassphrase = encryptedPassphrase,
                    NodePassphraseSignature = isSigningNodePassphraseRequired ? signature : null,
                    SignatureEmailAddress = isSigningNodePassphraseRequired ? signatureAddress.EmailAddress : null,
                    Name = name,
                    NameHash = nameHash,
                    NameSignatureEmailAddress = signatureAddress.EmailAddress,
                    OriginalNameHash = nodeToMove.NameHash,
                };

                await MoveNodeAsync(nodeToMove.Id, parameters, cancellationToken).ConfigureAwait(false);
            }

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

        return;

        static string GetNodeMediaType(RemoteNode node) => node is RemoteFile file ? file.MediaType : string.Empty;
    }

    public async Task DeleteAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        EnsureId(info.Id);

        cancellationToken.ThrowIfCancellationRequested();

        var remoteNode = await GetRemoteNodeAsync(info.Id, draftAllowed: true, cancellationToken).ConfigureAwait(false);
        CheckMetadata(remoteNode, info);
        CheckLink(remoteNode, info);

        await DeleteAsync(remoteNode, _folderApiClient.MoveChildrenToTrashAsync, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeletePermanentlyAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        EnsureId(info.Id);

        cancellationToken.ThrowIfCancellationRequested();

        var remoteNode = await GetRemoteNodeAsync(info.Id, draftAllowed: true, cancellationToken).ConfigureAwait(false);
        CheckMetadata(remoteNode, info);
        CheckLink(remoteNode, info);

        await DeleteAsync(remoteNode, _folderApiClient.DeleteChildrenAsync, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteRevisionAsync(NodeInfo<string> info, CancellationToken cancellationToken)
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
            await _fileApiClient.DeleteRevisionAsync(_shareId, remoteFile.Id, revisionId, cancellationToken)
                .ThrowOnFailure().ConfigureAwait(false);
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

    private async Task<AlbumCreationResponse> CreateAlbumAsync(AlbumCreationParameters parameters, CancellationToken cancellationToken)
    {
        try
        {
            return await _photoApiClient.CreateAlbumAsync(_volumeId, parameters, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(
                                       ex,
                                       id: null,
                                       includeObjectId: true,
                                       out var mappedException))
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

    private async Task AddToAlbumAsync(string albumLinkId, PhotoToAddListParameters parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (parameters.Photos.Count == 0)
        {
            _logger.LogWarning("No photos to add to album with ID {AlbumID}. Skipping request.", albumLinkId);
            return;
        }

        try
        {
            var response = await _photoApiClient.AddPhotosToAlbumAsync(_volumeId, albumLinkId, parameters, cancellationToken)
                .ThrowOnFailure()
                .ConfigureAwait(false);

            var failures = response.AddedPhotoResponses
                .Where(x => x.Response is { Succeeded: false })
                .ToList();

            var numberOfAlreadyExistsErrors = failures
                .Count(x => x.Response is { Code: ResponseCode.AlreadyExists });

            if (numberOfAlreadyExistsErrors > 0)
            {
                _logger.LogInformation(
                    "In current batch, {NumberOfFiles} Photo file(s) were already in the album with ID {AlbumId}",
                    numberOfAlreadyExistsErrors,
                    albumLinkId);
            }

            var numberOfMissingRelatedFilesErrors = failures
                .Count(x => x.Response is { Code: ResponseCode.MissingRelatedFiles });

            if (numberOfMissingRelatedFilesErrors > 0)
            {
                _logger.LogWarning(
                    "In current batch, {NumberOfFiles} Photo files(s) failed to add to the album with ID {AlbumId} due to missing related files. Failure is ignored",
                    numberOfMissingRelatedFilesErrors,
                    albumLinkId);
            }

            var numberOfInvalidRequirementsErrors = failures
                .Count(x => x.Response is { Code: ResponseCode.InvalidRequirements });

            if (numberOfInvalidRequirementsErrors > 0)
            {
                _logger.LogWarning(
                    "In current batch, {NumberOfFiles} Photo files(s) failed to add to the album with ID {AlbumId} due to invalid requirements. Failure is ignored",
                    numberOfInvalidRequirementsErrors,
                    albumLinkId);
            }

            var otherFailures = failures
                .Where(x => x.Response.Code is not
                    (ResponseCode.AlreadyExists or ResponseCode.MissingRelatedFiles or ResponseCode.InvalidRequirements))
                .ToList();

            foreach (var failure in otherFailures)
            {
                _logger.LogError(
                    "Adding Photo file with ID {FileID} to the album with ID {AlbumId} failed: {ErrorCode} {ErrorMessage}",
                    failure.LinkId,
                    albumLinkId,
                    failure.Response.Code,
                    failure.Response.Error);
            }

            var firstFailure = otherFailures.FirstOrDefault();

            if (firstFailure is not null)
            {
                throw new ApiException(firstFailure.Response.Code, firstFailure.Response.Error ?? "API request failed");
            }
        }
        catch (ApiException ex) when (ex.ResponseCode is ResponseCode.DoesNotExist or ResponseCode.InvalidRequirements or ResponseCode.TooManyChildren
                                      && ExceptionMapping.TryMapException(ex, id: null, includeObjectId: false, out var mappedException))
        {
            /* A specific case when the limit of photos is reached on the destination album or when the album does not exist */

            throw mappedException;
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, id: null, includeObjectId: false, out var mappedException))
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

    private async Task<IBlockVerifier> GetBlockVerifierAsync(string linkId, string revisionId, PgpPrivateKey nodeKey, CancellationToken cancellationToken)
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

    private async Task<(T Parameters, PgpPrivateKey NodeKey, Address SignatureAddress)> CreateNodeCreationParametersAsync<T>(
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
                parentFolder.PrivateKey.ToPublic(),
                signatureAddressId,
                cancellationToken).ConfigureAwait(false);

            var passphrase = _cryptographyService.GeneratePassphrase();
            var nodeKey = _cryptographyService.GenerateShareOrNodeKey();
            var (encryptedPassphrase, passphraseSignature, _) = parentEncrypter.EncryptShareOrNodeKeyPassphrase(passphrase);

            var parameters = factory.Invoke();

            parameters.Name = parentEncrypter.EncryptNodeName(info.Name);
            parameters.ParentLinkId = info.ParentId;
            parameters.NodeKey = nodeKey.Lock(passphrase.Span).ToString();
            parameters.NodePassphrase = encryptedPassphrase;
            parameters.NodePassphraseSignature = passphraseSignature;
            parameters.SignatureEmailAddress = signatureAddress.EmailAddress;
            parameters.NameHash = _cryptographyService.HashNodeNameHex(parentFolder.HashKey, info.Name);

            return (parameters, nodeKey, signatureAddress);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, info.ParentId, includeObjectId: false, out var mappedException))
        {
            throw mappedException;
        }
    }
}
