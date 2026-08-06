using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Client;

internal sealed class LocalFileRevision : ISourceRevision
{
    private static readonly byte[] ReadabilityCheckBuffer = new byte[1];

    private readonly FileSystemFile _file;
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly IFileMetadataGenerator _fileMetadataGenerator;
    private readonly IPhotoTagsGenerator _photoTagsGenerator;

    private Stream? _stream;

    public LocalFileRevision(
        FileSystemFile file,
        IThumbnailGenerator thumbnailGenerator,
        IFileMetadataGenerator fileMetadataGenerator,
        IPhotoTagsGenerator photoTagsGenerator)
    {
        _file = file;
        _thumbnailGenerator = thumbnailGenerator;
        _fileMetadataGenerator = fileMetadataGenerator;
        _photoTagsGenerator = photoTagsGenerator;

        Size = _file.Size;
        CreationTimeUtc = _file.CreationTimeUtc;
        LastWriteTimeUtc = _file.LastWriteTimeUtc;
    }

    public long Size { get; }
    public bool CanGetContentStream => true;
    public CancellationToken AbortionToken { get; } = CancellationToken.None;
    public DateTime CreationTimeUtc { get; }
    public DateTime LastWriteTimeUtc { get; }

    public async Task CheckReadabilityAsync(CancellationToken cancellationToken)
    {
        var stream = GetContentStream();

        _ = await stream.ReadAsync(ReadabilityCheckBuffer, cancellationToken).ConfigureAwait(false);
        stream.Seek(0, SeekOrigin.Begin);
    }

    public async Task<FileContentChecksum> GetContentChecksumAsync(CancellationToken cancellationToken)
    {
        var fileInfo = NodeInfo<long>.File().WithPath(_file.FullPath).WithId(_file.ObjectId);

        return await fileInfo.GetContentChecksumAsync(cancellationToken).ConfigureAwait(false);
    }

    public Stream GetContentStream()
    {
        return _stream ??= OpenContentStream();

        Stream OpenContentStream()
        {
            long fileId = 0;

            try
            {
                fileId = _file.ObjectId;

                return new SafeFileStream(_file.OpenRead(ownsHandle: false), fileId);
            }
            catch (Exception ex) when (ExceptionMapping.TryMapException(ex, fileId, out var mappedException))
            {
                throw mappedException;
            }
        }
    }

    public Task CopyContentToAsync(Stream destination, CancellationToken cancellationToken)
    {
        return GetContentStream().CopyToAsync(destination, cancellationToken);
    }

    public Task<ReadOnlyMemory<byte>?> TryGetThumbnailAsync(int numberOfPixelsOnLargestSide, int maxNumberOfBytes, CancellationToken cancellationToken)
    {
        return _thumbnailGenerator.TryGenerateThumbnailAsync(_file.FullPath, numberOfPixelsOnLargestSide, maxNumberOfBytes, cancellationToken);
    }

    public Task<FileMetadata?> GetMetadataAsync()
    {
        return _fileMetadataGenerator.GetMetadataAsync(_file.FullPath);
    }

    public Task<IReadOnlySet<PhotoTag>> GetPhotoTagsAsync(CancellationToken cancellationToken)
    {
        return _photoTagsGenerator.GetPhotoTagsAsync(_file.FullPath, cancellationToken);
    }

    public bool TryGetFileHasChanged(out bool hasChanged)
    {
        try
        {
            _file.Refresh();

            hasChanged = _file.Size != Size || _file.LastWriteTimeUtc != LastWriteTimeUtc;
        }
        catch
        {
            // Assume that the file has changed if it could not be refreshed
            hasChanged = true;
        }

        return true;
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _file.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();

        return ValueTask.CompletedTask;
    }
}
