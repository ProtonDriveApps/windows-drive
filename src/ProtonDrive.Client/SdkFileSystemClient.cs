using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk;
using Proton.Drive.Sdk.Nodes;
using ProtonDrive.Client.Configuration;
using ProtonDrive.Client.FileUploading;
using ProtonDrive.Client.MediaTypes;
using ProtonDrive.Client.RemoteNodes;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Features;
using ProtonDrive.Shared.IO;
using ProtonDrive.Shared.Metrics;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client;

/// <summary>
/// This client uses the Proton Drive SDK to access the remote file system.
/// </summary>
internal sealed class SdkFileSystemClient : RemoteFileSystemClientBase, IFileSystemClient<string>
{
    private readonly DriveApiConfig _apiConfig;
    private readonly ProtonDriveClient _sdkClient;
    private readonly IFileContentTypeProvider _fileContentTypeProvider;
    private readonly IFeatureFlagProvider _featureFlagProvider;
    private readonly Action<MetricEvent> _recordMetricEvent;
    private readonly Action<Exception> _reportIntegrityFailure;
    private readonly ILoggerFactory _loggerFactory;

    private readonly string _volumeId;
    private readonly string? _virtualParentId;
    private readonly string? _linkId;

    public SdkFileSystemClient(
        FileSystemClientParameters parameters,
        DriveApiConfig apiConfig,
        ProtonDriveClient sdkClient,
        IFileContentTypeProvider fileContentTypeProvider,
        IRemoteNodeService remoteNodeService,
        ILinkApiClient linkApiClient,
        IFeatureFlagProvider featureFlagProvider,
        Action<MetricEvent> recordMetricEvent,
        Action<Exception> reportIntegrityFailure,
        ILoggerFactory loggerFactory)
    : base(parameters, linkApiClient, remoteNodeService, fileContentTypeProvider)
    {
        _apiConfig = apiConfig;
        _sdkClient = sdkClient;
        _fileContentTypeProvider = fileContentTypeProvider;
        _featureFlagProvider = featureFlagProvider;
        _recordMetricEvent = recordMetricEvent;
        _reportIntegrityFailure = reportIntegrityFailure;
        _loggerFactory = loggerFactory;

        _volumeId = parameters.VolumeId;
        _virtualParentId = parameters.VirtualParentId;
        _linkId = parameters.LinkId;
    }

    public void Connect(string syncRootPath, IFileHydrationDemandHandler<string> fileHydrationDemandHandler)
    {
        // Do nothing
    }

    public Task DisconnectAsync()
    {
        return Task.CompletedTask;
    }

    public Task<NodeInfo<string>> GetInfoAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("SDK client implementation is not yet available");
    }

    public IAsyncEnumerable<NodeInfo<string>> EnumerateAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("SDK client implementation is not yet available");
    }

    public Task<NodeInfo<string>> CreateDirectoryAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("SDK client implementation is not yet available");
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

        CheckParentFolder(await GetRemoteNodeAsync(info.ParentId, cancellationToken).ConfigureAwait(false));

        if (!NodeUid.TryParse($"{_volumeId}~{info.ParentId}", out var parentNodeUid))
        {
            throw new FileSystemClientException("Invalid parent node UID", FileSystemErrorCode.Unknown);
        }

        var mediaType = _fileContentTypeProvider.GetContentType(info.Name);

        var metadata = await fileMetadataProvider.GetMetadataAsync().ConfigureAwait(false);

        var uploadMetadata = new FileUploadMetadata
        {
            LastModificationTime = info.LastWriteTimeUtc != default ? info.LastWriteTimeUtc : null,
            AdditionalMetadata = metadata.ConvertToAdditionalMetadataProperties(),
        };

        var fileUploader = await _sdkClient
            .GetFileUploaderAsync(
                parentNodeUid.Value,
                info.Name,
                mediaType,
                info.Size,
                uploadMetadata,
                overrideExistingDraftByOtherClient: false,
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var checksumVerificationEnabled = await _featureFlagProvider.UploadChecksumVerificationIsEnabledAsync(cancellationToken).ConfigureAwait(false);

            return new SdkRemoteRevisionCreationProcess(
                _apiConfig,
                fileUploader,
                info,
                checksumVerificationEnabled,
                thumbnailProvider,
                progressCallback,
                _recordMetricEvent,
                _reportIntegrityFailure,
                _loggerFactory.CreateLogger<SdkRemoteRevisionCreationProcess>());
        }
        catch
        {
            fileUploader.Dispose();
            throw;
        }
    }

    public async Task<ISourceRevision> OpenFileForReadingAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        EnsureId(info.Id);

        cancellationToken.ThrowIfCancellationRequested();

        var remoteFile = ToRemoteFile(await GetRemoteNodeAsync(info.Id, cancellationToken).ConfigureAwait(false));

        CheckMetadata(remoteFile, info);
        CheckLink(remoteFile, info);

        // Revision ID was not tracked in app versions before 1.4.0, fall back to the current active revision
        var revisionId = info.RevisionId ?? remoteFile.ActiveRevision?.Id;

        if (!RevisionUid.TryParse(_volumeId + "~" + info.Id + "~" + revisionId, out var revisionUid))
        {
            throw new FileSystemClientException(
                "Invalid file revision UID",
                FileSystemErrorCode.Unknown);
        }

        var fileDownloader = await _sdkClient.GetFileDownloaderAsync(revisionUid.Value, cancellationToken).ConfigureAwait(false);

        try
        {
            return new SdkRemoteFileRevision(
                _apiConfig,
                fileDownloader,
                remoteFile.CreationTime,
                remoteFile.ModificationTime,
                remoteFile.ExtendedAttributes,
                remoteFile.ActiveRevision?.ChecksumVerified,
                remoteFile.SizeOnStorage,
                _reportIntegrityFailure,
                _loggerFactory.CreateLogger<SdkRemoteFileRevision>());
        }
        catch
        {
            fileDownloader.Dispose();
            throw;
        }
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

        if (string.IsNullOrEmpty(info.RevisionId))
        {
            // File revision was not used in older app versions, its ID value might be missing.
            // We respond with metadata mismatch error, so that the remote file system adapter knows to refresh the file metadata.
            throw new FileSystemClientException<string>("File active revision is unknown", FileSystemErrorCode.MetadataMismatch, info.Id);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var remoteFile = ToRemoteFile(await GetRemoteNodeAsync(info.Id, cancellationToken).ConfigureAwait(false));
        CheckMetadata(remoteFile, info);
        CheckLink(remoteFile, info);

        if (!RevisionUid.TryParse($"{_volumeId}~{info.Id}~{info.RevisionId}", out var activeRevisionUid))
        {
            throw new FileSystemClientException("Invalid active revision UID", FileSystemErrorCode.Unknown);
        }

        var metadata = await fileMetadataProvider.GetMetadataAsync().ConfigureAwait(false);

        var uploadMetadata = new FileUploadMetadata
        {
            LastModificationTime = lastWriteTime,
            AdditionalMetadata = metadata.ConvertToAdditionalMetadataProperties(),
        };

        var fileUploader = await _sdkClient
            .GetFileRevisionUploaderAsync(
                activeRevisionUid.Value,
                size,
                uploadMetadata,
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var nodeInfo = info.Copy()
                .WithParentId(_linkId is null ? remoteFile.ParentId : _virtualParentId)
                .WithSize(size)
                .WithLastWriteTimeUtc(lastWriteTime);

            var checksumVerificationEnabled = await _featureFlagProvider.UploadChecksumVerificationIsEnabledAsync(cancellationToken).ConfigureAwait(false);

            return new SdkRemoteRevisionCreationProcess(
                _apiConfig,
                fileUploader,
                nodeInfo,
                checksumVerificationEnabled,
                thumbnailProvider,
                progressCallback,
                _recordMetricEvent,
                _reportIntegrityFailure,
                _loggerFactory.CreateLogger<SdkRemoteRevisionCreationProcess>());
        }
        catch
        {
            fileUploader.Dispose();
            throw;
        }
    }

    public Task MoveAsync(IReadOnlyList<NodeInfo<string>> sourceNodes, NodeInfo<string> destinationInfo, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("SDK client implementation is not yet available");
    }

    public Task MoveAsync(NodeInfo<string> info, NodeInfo<string> destinationInfo, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("SDK client implementation is not yet available");
    }

    public Task DeleteAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("SDK client implementation is not yet available");
    }

    public Task DeletePermanentlyAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("SDK client implementation is not yet available");
    }

    public Task DeleteRevisionAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
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

    private static void CheckParentFolder(RemoteNode remoteNode)
    {
        ToRemoteFolder(remoteNode);
    }
}
