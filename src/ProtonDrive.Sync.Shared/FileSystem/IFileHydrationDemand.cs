using System;
using System.IO;

namespace ProtonDrive.Sync.Shared.FileSystem;

public interface IFileHydrationDemand<TId>
    where TId : IEquatable<TId>
{
    NodeInfo<TId> FileInfo { get; }
    Stream HydrationStream { get; }

    NodeInfo<TId> UpdateFileSize();
}
