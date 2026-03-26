using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.Photos.Import;

internal sealed class PhotoFileUploader : IPhotoFileUploader
{
    private readonly IPhotoFileSystemClient<long> _localFileSystemClient;
    private readonly IFileSystemClient<string> _remoteFileSystemClient;
    private readonly ILogger<PhotoFileUploader> _logger;

    public PhotoFileUploader(
        IPhotoFileSystemClient<long> localFileSystemClient,
        IFileSystemClient<string> remoteFileSystemClient,
        ILogger<PhotoFileUploader> logger)
    {
        _localFileSystemClient = localFileSystemClient;
        _remoteFileSystemClient = remoteFileSystemClient;
        _logger = logger;
    }

    public async Task<NodeInfo<string>> UploadFileAsync(string filePath, string parentLinkId, string? mainPhotoLinkId, CancellationToken cancellationToken)
    {
        var nodeInfo = NodeInfo<long>.File().WithPath(filePath);
        var sourceRevision = await _localFileSystemClient.OpenFileForReadingAsync(nodeInfo, cancellationToken).ConfigureAwait(false);

        await using (sourceRevision.ConfigureAwait(false))
        {
            var remoteNodeInfo = NodeInfo<string>.File()
                .WithParentId(parentLinkId)
                .WithMainPhotoLinkId(mainPhotoLinkId)
                .WithPath(filePath)
                .WithName(Path.GetFileName(filePath))
                .WithSize(sourceRevision.Size)
                .WithLastWriteTimeUtc(sourceRevision.LastWriteTimeUtc);

            var destinationRevision = await _remoteFileSystemClient.CreateFileAsync(
                remoteNodeInfo,
                tempFileName: null,
                sourceRevision,
                sourceRevision,
                progressCallback: null,
                cancellationToken).ConfigureAwait(false);

            await using (destinationRevision.ConfigureAwait(false))
            {
                var result = await FinishRevisionCreationAsync(sourceRevision, destinationRevision, cancellationToken).ConfigureAwait(false);
                return result;
            }
        }
    }

    private static async Task<NodeInfo<string>> FinishRevisionCreationAsync(
        ISourceRevision sourceRevision,
        IDestinationRevision<string> destinationRevision,
        CancellationToken cancellationToken)
    {
        var sha1 = await sourceRevision.GetSha1Async(cancellationToken).ConfigureAwait(false);

        await destinationRevision.WriteContentAsync(sourceRevision.GetContentStream(), sha1, cancellationToken).ConfigureAwait(false);

        return await destinationRevision.FinishAsync(sha1, cancellationToken).ConfigureAwait(false);
    }
}
