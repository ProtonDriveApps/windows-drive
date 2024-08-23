using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Sync.Shared.FileSystem;

public interface IRevision : IThumbnailProvider, IDisposable, IAsyncDisposable
{
    long Size { get; }
    DateTime LastWriteTimeUtc { get; }

    Task CheckReadabilityAsync(CancellationToken cancellationToken);
    Stream GetContentStream();
    bool TryGetFileHasChanged(out bool hasChanged);
}
