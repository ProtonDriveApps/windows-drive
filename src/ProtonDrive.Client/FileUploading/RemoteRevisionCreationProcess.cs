using System.Security.Cryptography;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client.FileUploading;

internal class RemoteRevisionCreationProcess : IDestinationRevision<string>
{
    private readonly HashingStream _destinationStream;
    private readonly IReadOnlyCollection<UploadedBlock> _uploadedBlocks;
    private readonly int _blockSize;
    private readonly IRevisionSealer _revisionSealer;

    public RemoteRevisionCreationProcess(
        NodeInfo<string> fileInfo,
        bool checksumVerificationEnabled,
        Stream destinationStream,
        IReadOnlyCollection<UploadedBlock> uploadedBlocks,
        int blockSize,
        IRevisionSealer revisionSealer)
    {
        Ensure.NotNull(fileInfo.Id, nameof(fileInfo), nameof(fileInfo.Id));

        FileInfo = fileInfo;
        ChecksumVerificationEnabled = checksumVerificationEnabled;

        _destinationStream = new HashingStream(
            new SafeRemoteFileStream(destinationStream, FileInfo.Id),
            HashAlgorithmName.SHA1);

        _uploadedBlocks = uploadedBlocks;
        _blockSize = blockSize;

        _revisionSealer = revisionSealer;
    }

    public NodeInfo<string> FileInfo { get; }
    public NodeInfo<string> BackupInfo { get; set; } = NodeInfo<string>.Empty();
    public bool ImmediateHydrationRequired => true;
    public bool ChecksumVerificationEnabled { get; }
    public virtual bool CanGetContentStream => true;

    public virtual Stream GetContentStream()
    {
        // The Drive encrypted file write stream requires the Length to be set before writing the content
        _destinationStream.SetLength(FileInfo.Size);

        return _destinationStream;
    }

    public virtual async Task WriteContentAsync(Stream source, ReadOnlyMemory<byte>? expectedSha1, CancellationToken cancellationToken)
    {
        var destination = GetContentStream();

        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

        if (destination.Position != destination.Length)
        {
            // TODO: throw meaningful exception here instead of relying on RemoteFileWriteStream to do that.
            destination.SetLength(destination.Position);
        }

        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<NodeInfo<string>> FinishAsync(ReadOnlyMemory<byte>? expectedSha1, CancellationToken cancellationToken)
    {
        try
        {
            ValidateUpload(expectedSha1);

            var revisionSealingParameters = GetRevisionSealingParameters();

            await _revisionSealer.SealRevisionAsync(revisionSealingParameters, cancellationToken)
                .ConfigureAwait(false);

            return FileInfo.Copy().WithSizeOnStorage(_uploadedBlocks.Sum(b => (long)b.Size)).WithSha1Digest(revisionSealingParameters.Sha1Digest);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, FileInfo.Id, includeObjectId: false, out var mappedException))
        {
            throw mappedException;
        }
    }

    public ValueTask DisposeAsync()
    {
        return _destinationStream.DisposeAsync();
    }

    protected virtual RevisionSealingParameters GetRevisionSealingParameters()
    {
        return new RevisionSealingParameters
        {
            Blocks = _uploadedBlocks,
            Sha1Digest = Convert.ToHexStringLower(GetContentSha1()),
        };
    }

    private void ValidateUpload(ReadOnlyMemory<byte>? expectedSha1)
    {
        var expectedNumberOfContentBlocks = (FileInfo.Size + _blockSize - 1) / _blockSize;

        var numberOfUploadedContentBlocks = 0;
        var numberOfPlainDataBytesRead = 0L;
        foreach (var block in _uploadedBlocks.Where(x => !x.IsThumbnail))
        {
            ++numberOfUploadedContentBlocks;
            numberOfPlainDataBytesRead += block.NumberOfPlainDataBytesRead;
        }

        if (numberOfPlainDataBytesRead != FileInfo.Size)
        {
            throw new FileSystemClientException("The number of bytes read from the file does not equal the expected size", FileSystemErrorCode.IntegrityFailure);
        }

        if (numberOfUploadedContentBlocks != expectedNumberOfContentBlocks)
        {
            throw new FileSystemClientException("The number of uploaded blocks does not equal the expected number", FileSystemErrorCode.IntegrityFailure);
        }

        if (ChecksumVerificationEnabled && expectedSha1?.Span.SequenceEqual(GetContentSha1()) == false)
        {
            throw new FileSystemClientException("The uploaded file checksum does not match the expected checksum", FileSystemErrorCode.IntegrityFailure);
        }
    }

    private byte[] GetContentSha1()
    {
        var sha1 = new byte[SHA1.HashSizeInBytes];
        _destinationStream.GetCurrentHash(sha1.AsSpan());
        return sha1;
    }
}
