using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Sync.Shared.FileSystem;

public interface IRevisionCreationProcess<TId> : IAsyncDisposable
    where TId : IEquatable<TId>
{
    NodeInfo<TId> FileInfo { get; }
    NodeInfo<TId> BackupInfo { get; set; }
    bool ImmediateHydrationRequired { get; }

    Stream OpenContentStream();
    Task<NodeInfo<TId>> FinishAsync(CancellationToken cancellationToken);
}
