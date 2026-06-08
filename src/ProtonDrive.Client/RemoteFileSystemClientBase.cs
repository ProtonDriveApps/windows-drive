using System.Diagnostics.CodeAnalysis;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.MediaTypes;
using ProtonDrive.Client.RemoteNodes;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client;

internal abstract class RemoteFileSystemClientBase
{
    private readonly ILinkApiClient _linkApiClient;
    private readonly IRemoteNodeService _remoteNodeService;
    private readonly IFileContentTypeProvider _fileContentTypeProvider;
    private readonly string _shareId;
    private readonly string? _linkId;
    private readonly string? _virtualParentId;

    internal RemoteFileSystemClientBase(
        FileSystemClientParameters fileSystemClientParameters,
        ILinkApiClient linkApiClient,
        IRemoteNodeService remoteNodeService,
        IFileContentTypeProvider fileContentTypeProvider)
    {
        _shareId = fileSystemClientParameters.ShareId;
        _linkId = fileSystemClientParameters.LinkId;
        _virtualParentId = fileSystemClientParameters.VirtualParentId;
        _linkApiClient = linkApiClient;
        _remoteNodeService = remoteNodeService;
        _fileContentTypeProvider = fileContentTypeProvider;
    }

    internal async Task<Share> GetShareAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _remoteNodeService.GetShareAsync(_shareId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, id: null, includeObjectId: false, out var mappedException))
        {
            throw mappedException;
        }
    }

    internal Task<RemoteNode> GetRemoteNodeAsync(string linkId, CancellationToken cancellationToken)
    {
        return GetRemoteNodeAsync(linkId, draftAllowed: false, cancellationToken);
    }

    internal async Task<RemoteNode> GetRemoteNodeAsync(string linkId, bool draftAllowed, CancellationToken cancellationToken)
    {
        try
        {
            var linkResponse = await _linkApiClient.GetLinkAsync(_shareId, linkId, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

            var link = linkResponse.Link ?? throw new ApiException(ResponseCode.InvalidValue, "Link is not present in the API response");

            return await GetRemoteNodeAsync(link, draftAllowed, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, linkId, includeObjectId: true, out var mappedException))
        {
            throw mappedException;
        }
    }

    internal async Task<RemoteNode> GetRemoteNodeAsync(Link link, bool draftAllowed, CancellationToken cancellationToken)
    {
        try
        {
            if (link.State is not LinkState.Active && (!draftAllowed || link.State is not LinkState.Draft))
            {
                throw new FileSystemClientException<string>($"File system object state is {link.State}", FileSystemErrorCode.ObjectNotFound, link.Id);
            }

            if (link.Id == _linkId)
            {
                link = link with { ParentId = _virtualParentId };
            }

            return await DecryptLinkAsync(link, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, link.Id, includeObjectId: true, out var mappedException))
        {
            throw mappedException;
        }
    }

    internal async Task<RemoteNode> DecryptLinkAsync(Link link, IPrivateKeyHolder parent, CancellationToken cancellationToken)
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

    internal async Task<RemoteNode> DecryptLinkAsync(Link link, CancellationToken cancellationToken)
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

    internal async Task<RemoteFolder> GetKeyHolderAsync(string parentLinkId, CancellationToken cancellationToken)
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

    protected string? GetMediaType(NodeInfo<string> nodeInfo)
    {
        return nodeInfo.IsDirectory()
            ? null :
            _fileContentTypeProvider.GetContentType(nodeInfo.Name);
    }

    protected static RemoteFolder ToRemoteFolder(RemoteNode node)
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

    protected static RemoteFile ToRemoteFile(RemoteNode node)
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

    protected static void EnsureId([NotNull] string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new FileSystemClientException<string>(
                "Node Id value is not specified",
                FileSystemErrorCode.PathBasedAccessNotSupported,
                objectId: null);
        }
    }

    protected static void EnsureParentId([NotNull] string? parentId)
    {
        if (string.IsNullOrEmpty(parentId))
        {
            throw new FileSystemClientException<string>(
                "Parent node Id value is not specified",
                FileSystemErrorCode.PathBasedAccessNotSupported,
                objectId: null);
        }
    }

    protected static void CheckLink(RemoteNode remoteNode, NodeInfo<string> info)
    {
        if (!string.IsNullOrEmpty(info.ParentId) && remoteNode.ParentId != info.ParentId)
        {
            throw new FileSystemClientException<string>(
                $"Client-side optimistic locking failure: Parent link has diverged, expected {info.ParentId} but found {remoteNode.ParentId}",
                FileSystemErrorCode.MetadataMismatch,
                info.Id);
        }

        if (!string.IsNullOrEmpty(info.Name) && !remoteNode.MatchesRemoteName(info.Name))
        {
            throw new FileSystemClientException<string>(
                "Client-side optimistic locking failure: Node name has diverged",
                FileSystemErrorCode.MetadataMismatch,
                info.Id);
        }
    }

    protected static void CheckMetadata(RemoteNode remoteNode, NodeInfo<string> info)
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

    protected static bool IsFileRevisionExpected(RemoteNode remoteNode, NodeInfo<string> info)
    {
        if (remoteNode is not RemoteFile remoteFile || info.RevisionId == null)
        {
            return true;
        }

        return remoteFile.ActiveRevision?.Id == info.RevisionId;
    }

    protected static bool IsModificationTimeExpected(RemoteNode remoteNode, NodeInfo<string> info)
    {
        // Modification time is used as Folder last write time.
        // Modification time for files is not checked in favor of checking Revision ID.
        if (remoteNode is not RemoteFolder || info.LastWriteTimeUtc == default)
        {
            return true;
        }

        return remoteNode.ModificationTime == info.LastWriteTimeUtc;
    }

    protected static bool IsFileSizeExpected(RemoteNode remoteNode, NodeInfo<string> info)
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
}
