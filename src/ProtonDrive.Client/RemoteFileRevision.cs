using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client;

internal sealed class RemoteFileRevision : IRevision
{
    private readonly Stream _contentStream;

    public RemoteFileRevision(Stream contentStream, DateTime lastWriteTimeUtc)
    {
        _contentStream = contentStream;
        LastWriteTimeUtc = lastWriteTimeUtc;
    }

    public long Size => _contentStream.Length;
    public DateTime LastWriteTimeUtc { get; }

    public Task CheckReadabilityAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Stream GetContentStream()
    {
        return _contentStream;
    }

    public bool TryGetFileHasChanged(out bool hasChanged)
    {
        hasChanged = false;
        return false;
    }

    public bool TryGetThumbnail(int numberOfPixelsOnLargestSide, int maxNumberOfBytes, out ReadOnlyMemory<byte> thumbnailBytes)
    {
        thumbnailBytes = ReadOnlyMemory<byte>.Empty;
        return false;
    }

    public void Dispose()
    {
        _contentStream.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _contentStream.DisposeAsync();
    }
}
