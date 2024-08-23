using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client.FileUploading;

internal sealed class RemoteRevisionCreationProcess : IRevisionCreationProcess<string>
{
    private readonly Stream _contentStream;
    private readonly IReadOnlyCollection<UploadedBlock> _uploadedBlocks;
    private readonly int _blockSize;
    private readonly IRevisionSealer _revisionSealer;

    public RemoteRevisionCreationProcess(
        NodeInfo<string> fileInfo,
        Stream contentStream,
        IReadOnlyCollection<UploadedBlock> uploadedBlocks,
        int blockSize,
        IRevisionSealer revisionSealer)
    {
        Ensure.NotNull(fileInfo.Id, nameof(fileInfo), nameof(fileInfo.Id));

        FileInfo = fileInfo;

        _contentStream = contentStream;
        _uploadedBlocks = uploadedBlocks;
        _blockSize = blockSize;

        _revisionSealer = revisionSealer;
    }

    public NodeInfo<string> FileInfo { get; }
    public NodeInfo<string> BackupInfo { get; set; } = NodeInfo<string>.Empty();
    public bool ImmediateHydrationRequired => true;

    public Stream OpenContentStream()
    {
        return new SafeRemoteFileStream(_contentStream, FileInfo.Id);
    }

    public async Task<NodeInfo<string>> FinishAsync(CancellationToken cancellationToken)
    {
        try
        {
            ValidateUpload();

            await _revisionSealer.SealRevisionAsync(_uploadedBlocks, cancellationToken).ConfigureAwait(false);

            return FileInfo.Copy().WithSizeOnStorage(_uploadedBlocks.Sum(b => (long)b.Size));
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, FileInfo.Id, includeObjectId: false, out var mappedException))
        {
            throw mappedException;
        }
    }

    public void Dispose()
    {
        _contentStream.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _contentStream.DisposeAsync();
    }

    private void ValidateUpload()
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
    }
}
