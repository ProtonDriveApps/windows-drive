using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

internal sealed class FileRevision : IRevision
{
    private static readonly byte[] ReadabilityCheckBuffer = new byte[1];

    private readonly FileSystemFile _file;
    private readonly IThumbnailGenerator _thumbnailGenerator;

    private Stream? _stream;

    public FileRevision(FileSystemFile file, IThumbnailGenerator thumbnailGenerator)
    {
        _file = file;
        _thumbnailGenerator = thumbnailGenerator;

        Size = _file.Size;
        LastWriteTimeUtc = _file.LastWriteTimeUtc;
    }

    public long Size { get; }
    public DateTime LastWriteTimeUtc { get; }

    public async Task CheckReadabilityAsync(CancellationToken cancellationToken)
    {
        var stream = GetContentStream();

        _ = await stream.ReadAsync(ReadabilityCheckBuffer, cancellationToken).ConfigureAwait(false);
        stream.Seek(0, SeekOrigin.Begin);
    }

    public Stream GetContentStream()
    {
        return _stream ??= OpenContentStream();

        Stream OpenContentStream()
        {
            long fileId = default;

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

    public bool TryGetThumbnail(int numberOfPixelsOnLargestSide, int maxNumberOfBytes, out ReadOnlyMemory<byte> thumbnailBytes)
    {
        return _thumbnailGenerator.TryGenerateThumbnail(_file.FullPath, numberOfPixelsOnLargestSide, maxNumberOfBytes, out thumbnailBytes);
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
        _file.Dispose();
        _stream?.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();

        return ValueTask.CompletedTask;
    }
}
