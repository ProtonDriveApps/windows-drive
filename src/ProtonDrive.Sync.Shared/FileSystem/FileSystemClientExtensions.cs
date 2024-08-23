using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Sync.Shared.FileSystem;

public static class FileSystemClientExtensions
{
    public static Task Rename<TId>(this IFileSystemClient<TId> origin, NodeInfo<TId> info, string newName, CancellationToken cancellationToken)
        where TId : IEquatable<TId>
    {
        Ensure.NotNullOrEmpty(newName, nameof(newName));
        Ensure.NotNullOrEmpty(info.Path, nameof(info), nameof(info.Path));

        var newInfo = info.Copy()
            .WithName(newName)
            .WithPath(Path.Combine(Path.GetDirectoryName(info.Path) ?? string.Empty, newName));

        return origin.Move(info, newInfo, cancellationToken);
    }
}
