using Microsoft.Extensions.Logging;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client.FileUploading;

internal sealed class RemotePhotoRevisionCreationProcess : RemoteRevisionCreationProcess
{
    private readonly DateTime _creationTimeUtc;
    private readonly DateTime _lastWriteTimeUtc;
    private readonly ILogger<RemotePhotoRevisionCreationProcess> _logger;

    public RemotePhotoRevisionCreationProcess(
        NodeInfo<string> fileInfo,
        bool checksumVerificationEnabled,
        Stream contentStream,
        IReadOnlyCollection<UploadedBlock> uploadedBlocks,
        int blockSize,
        DateTime creationTimeUtc,
        DateTime lastWriteTimeUtc,
        IRevisionSealer revisionSealer,
        ILogger<RemotePhotoRevisionCreationProcess> logger)
        : base(fileInfo, checksumVerificationEnabled, contentStream, uploadedBlocks, blockSize, revisionSealer)
    {
        _creationTimeUtc = creationTimeUtc;
        _lastWriteTimeUtc = lastWriteTimeUtc;
        _logger = logger;
    }

    public override bool CanGetContentStream => false;

    public override Stream GetContentStream() => throw new NotSupportedException();

    public override async Task WriteContentAsync(Stream source, FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
    {
        var destination = base.GetContentStream();

        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

        if (destination.Position != FileInfo.Size)
        {
            // The source stream provided less data than it was declared
            _logger.LogWarning(
                "File size changed while being uploaded: " +
                "destination position {Position:N0} differs from destination length {Length:N0} " +
                "(source position {SourcePosition:N0}, current source length {SourceLength:N0})",
                destination.Position,
                destination.Length,
                source.Position,
                source.Length);

            throw new PhotoFileSizeMismatchException(
                "Failed to import file due to file size mismatch: " +
                $"source {source.Length:N0} bytes, expected {destination.Length:N0} bytes, got {destination.Position:N0} bytes.");
        }

        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override RevisionSealingParameters GetRevisionSealingParameters()
    {
        var revisionSealingParameters = base.GetRevisionSealingParameters();

        var defaultCaptureTimeUtc = _creationTimeUtc < _lastWriteTimeUtc ? _creationTimeUtc : _lastWriteTimeUtc;

        return new PhotoRevisionSealingParameters
        {
            Blocks = revisionSealingParameters.Blocks,
            Sha1Digest = revisionSealingParameters.Sha1Digest,
            DefaultCaptureTimeUtc = defaultCaptureTimeUtc,
            MainPhotoLinkId = FileInfo.MainPhotoLinkId,
        };
    }
}
