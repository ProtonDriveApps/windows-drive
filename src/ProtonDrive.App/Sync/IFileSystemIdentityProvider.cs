using System.Diagnostics.CodeAnalysis;

namespace ProtonDrive.App.Sync;

public interface IFileSystemIdentityProvider<TId>
{
    bool TryGetIdFromPath(string path, [MaybeNullWhen(false)] out TId id);
}
