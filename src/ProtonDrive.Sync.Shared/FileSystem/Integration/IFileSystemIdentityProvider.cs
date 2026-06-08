using System.Diagnostics.CodeAnalysis;

namespace ProtonDrive.Sync.Shared.FileSystem.Integration;

public interface IFileSystemIdentityProvider<TId>
{
    bool TryGetIdFromPath(string path, [MaybeNullWhen(false)] out TId id);
}
