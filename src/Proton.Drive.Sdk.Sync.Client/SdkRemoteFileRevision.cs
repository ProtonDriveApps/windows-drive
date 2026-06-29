using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Nodes.Download;
using Proton.Drive.Sdk.Sync.Client.Configuration;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Client.Sdk;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;

namespace Proton.Drive.Sdk.Sync.Client;

internal sealed class SdkRemoteFileRevision : ISourceRevision
{
    private static readonly Action<long, long?> NullProgressCallback = (_, _) => { };

    private readonly DriveApiConfig _apiConfig;
    private readonly FileDownloader _fileDownloader;
    private readonly ExtendedAttributes? _extendedAttributes;
    private readonly bool? _checksumVerified;
    private readonly Action<Exception> _reportIntegrityFailure;
    private readonly ILogger<SdkRemoteFileRevision> _logger;

    public SdkRemoteFileRevision(
        DriveApiConfig apiConfig,
        FileDownloader fileDownloader,
        DateTime creationTimeUtc,
        DateTime lastWriteTimeUtc,
        ExtendedAttributes? extendedAttributes,
        bool? checksumVerified,
        long sizeOnStorage,
        Action<Exception> reportIntegrityFailure,
        ILogger<SdkRemoteFileRevision> logger)
    {
        _apiConfig = apiConfig;
        _fileDownloader = fileDownloader;
        CreationTimeUtc = creationTimeUtc;
        LastWriteTimeUtc = lastWriteTimeUtc;
        _extendedAttributes = extendedAttributes;
        _checksumVerified = checksumVerified;
        _reportIntegrityFailure = reportIntegrityFailure;
        _logger = logger;

        Size = _extendedAttributes?.Common?.Size ?? sizeOnStorage;
    }

    public long Size { get; }
    public bool CanGetContentStream => false;
    public CancellationToken AbortionToken { get; } = CancellationToken.None;
    public DateTime CreationTimeUtc { get; }
    public DateTime LastWriteTimeUtc { get; }

    public Stream GetContentStream()
    {
        throw new NotSupportedException("SDK-based remote file revision does not provide a stream");
    }

    public Task CheckReadabilityAsync(CancellationToken cancellationToken)
    {
        // Assume the remote file is always readable
        return Task.CompletedTask;
    }

    public Task<FileContentChecksum> GetContentChecksumAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(GetChecksum());

        FileContentChecksum GetChecksum()
        {
            var hexSha1 = _extendedAttributes?.Common?.Digests?.Sha1;

            if (string.IsNullOrEmpty(hexSha1))
            {
                return FileContentChecksum.Empty;
            }

            try
            {
                var sha1 = Convert.FromHexString(hexSha1);

                return sha1.Length == SHA1.HashSizeInBytes
                    ? new FileContentChecksum { Sha1 = sha1, Sha1Verified = _checksumVerified ?? false }
                    : FileContentChecksum.Empty;
            }
            catch (FormatException)
            {
                return FileContentChecksum.Empty;
            }
        }
    }

    public async Task CopyContentToAsync(Stream destination, CancellationToken cancellationToken)
    {
        DownloadController? controller = null;

        try
        {
            controller = _fileDownloader.DownloadToStream(destination, NullProgressCallback, cancellationToken);

            await using (controller.ConfigureAwait(false))
            {
                await controller.ExecuteWithRetryAsync(
                    _apiConfig.FileTransferNumberOfRetries,
                    _apiConfig.FileTransferDelayBetweenRetries,
                    _logger,
                    cancellationToken).ConfigureAwait(false);

                await controller.Completion.ConfigureAwait(false);
            }
        }
        catch (DataIntegrityException) when (controller?.GetIsDownloadCompleteWithVerificationIssue() == true)
        {
            // Content download succeeded, but the revision manifest signature verification produced non-successful result
        }
        catch (Exception ex) when (ExceptionMapping.TryMapSdkClientException(ex, id: null, includeObjectId: false, out var mappedException))
        {
            if (ExceptionMapping.IsSdkIntegrityFailure(ex))
            {
                _reportIntegrityFailure.Invoke(ex);
            }

            throw mappedException;
        }
    }

    public bool TryGetFileHasChanged(out bool hasChanged)
    {
        hasChanged = false;
        return false;
    }

    public Task<ReadOnlyMemory<byte>?> TryGetThumbnailAsync(int numberOfPixelsOnLargestSide, int maxNumberOfBytes, CancellationToken cancellationToken)
    {
        return Task.FromResult<ReadOnlyMemory<byte>?>(null);
    }

    public Task<FileMetadata?> GetMetadataAsync()
    {
        if (_extendedAttributes is null)
        {
            return Task.FromResult(default(FileMetadata?));
        }

        var metadata = FileMetadataSanitizer.GetFileMetadata(
            _extendedAttributes.Media?.Width,
            _extendedAttributes.Media?.Height,
            _extendedAttributes.Media?.Duration,
            _extendedAttributes.Camera?.Orientation,
            _extendedAttributes.Camera?.Device,
            _extendedAttributes.Camera?.CaptureTime,
            _extendedAttributes.Location?.Latitude,
            _extendedAttributes.Location?.Longitude);

        return Task.FromResult(metadata);
    }

    public Task<IReadOnlySet<PhotoTag>> GetPhotoTagsAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public void Dispose()
    {
        _fileDownloader.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
