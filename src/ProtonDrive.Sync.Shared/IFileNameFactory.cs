using System;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.Sync.Shared;

public interface IFileNameFactory<TId> where TId : IEquatable<TId>
{
    string GetName(IFileSystemNodeModel<TId> nodeModel);
}
