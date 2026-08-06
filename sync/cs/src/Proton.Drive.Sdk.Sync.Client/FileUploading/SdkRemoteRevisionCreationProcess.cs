using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Nodes.Upload;
using Proton.Drive.Sdk.Sync.Client.Configuration;
using Proton.Drive.Sdk.Sync.Client.Sdk;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.IO;
using Proton.Drive.Shared.Metrics;

namespace Proton.Drive.Sdk.Sync.Client.FileUploading;

internal sealed class SdkRemoteRevisionCreationProcess : IDestinationRevision<string>
{
    private readonly DriveApiConfig _apiConfig;
    private readonly FileUploader _fileUploader;
    private readonly IThumbnailProvider _thumbnailProvider;
    private readonly Action<Progress>? _progressCallback;
    private readonly Action<MetricEvent> _recordMetricEvent;
    private readonly ILogger<SdkRemoteRevisionCreationProcess> _logger;

    public SdkRemoteRevisionCreationProcess(
        DriveApiConfig apiConfig,
        FileUploader fileUploader,
        NodeInfo<string> fileInfo,
        bool checksumVerificationEnabled,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        Action<MetricEvent> recordMetricEvent,
        ILogger<SdkRemoteRevisionCreationProcess> logger)
    {
        _apiConfig = apiConfig;
        _fileUploader = fileUploader;
        FileInfo = fileInfo;
        ChecksumVerificationEnabled = checksumVerificationEnabled;
        _thumbnailProvider = thumbnailProvider;
        _progressCallback = progressCallback;
        _recordMetricEvent = recordMetricEvent;
        _logger = logger;
    }

    public NodeInfo<string> FileInfo { get; private set; }
    public NodeInfo<string> BackupInfo { get; set; } = NodeInfo<string>.Empty();
    public bool ImmediateHydrationRequired => true;
    public bool ChecksumVerificationEnabled { get; }
    public bool CanGetContentStream => false;

    public Stream GetContentStream()
    {
        throw new NotSupportedException();
    }

    public async Task WriteContentAsync(Stream contentStream, FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
    {
        var thumbnails = await _thumbnailProvider.GetThumbnailsAsync(cancellationToken).ConfigureAwait(false);

        Func<ReadOnlyMemory<byte>>? expectedSha1Provider = ChecksumVerificationEnabled && expectedChecksum.Sha1 is not null ? () => expectedChecksum.Sha1.Value : null;

        RecordChecksumVerificationAttempt(expectedSha1Provider);

        try
        {
            var controller = _fileUploader.UploadFromStream(
                contentStream,
                thumbnails,
                (progress, total) => _progressCallback?.Invoke(new Progress(progress, total)),
                expectedSha1Provider,
                forPhotos: false,
                cancellationToken);

            await using (controller.ConfigureAwait(false))
            {
                await controller.ExecuteWithRetryAsync(
                    _apiConfig.FileTransferNumberOfRetries,
                    _apiConfig.FileTransferDelayBetweenRetries,
                    _logger,
                    cancellationToken).ConfigureAwait(false);

                var (fileNodeUid, fileRevisionUid) = await controller.Completion.ConfigureAwait(false);

                // NOTE: Sha1Digest and SizeOnStorage are not available when using SDK
                FileInfo = FileInfo.Copy()
                    .WithId(fileNodeUid.ToLinkId())
                    .WithRevisionId(fileRevisionUid.ToRevisionId());
            }
        }
        catch (Exception ex) when (ExceptionMapping.TryMapSdkClientException(ex, FileInfo.Id, includeObjectId: false, out var mappedException))
        {
            throw mappedException;
        }
    }

    public Task<NodeInfo<string>> FinishAsync(FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
    {
        return Task.FromResult(FileInfo);
    }

    public ValueTask DisposeAsync()
    {
        _fileUploader.Dispose();

        return ValueTask.CompletedTask;
    }

    private void RecordChecksumVerificationAttempt(Func<ReadOnlyMemory<byte>>? expectedSha1Provider)
    {
        var sha1Provided = expectedSha1Provider != null;

        _recordMetricEvent.Invoke(new UploadChecksumVerificationAttemptEvent
        {
            Sha1Provided = sha1Provided,
        });
    }
}
