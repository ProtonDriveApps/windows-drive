using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Sync.Shared.FileSystem;

public static class FileSystemClientExtensions
{
    public static Task RenameAsync<TId>(this IFileSystemClient<TId> origin, NodeInfo<TId> info, string newName, CancellationToken cancellationToken)
        where TId : IEquatable<TId>
    {
        Ensure.NotNullOrEmpty(newName, nameof(newName));
        Ensure.IsTrue(
            string.IsNullOrEmpty(info.Path) || (info.Path.EndsWith(info.Name, StringComparison.Ordinal) && info.Path[^info.Name.Length].Equals(Path.DirectorySeparatorChar)),
            "Path and name do not match",
            nameof(info));

        var newPath = !string.IsNullOrEmpty(info.Path)
            ? Path.Combine(info.Path[..^(info.Name.Length + 1)], newName)
            : string.Empty;

        var newInfo = info.Copy()
            .WithName(newName)
            .WithPath(newPath);

        return origin.MoveAsync(info, newInfo, cancellationToken);
    }
}
