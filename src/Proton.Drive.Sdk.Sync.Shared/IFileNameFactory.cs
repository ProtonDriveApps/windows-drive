using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Shared;

public interface IFileNameFactory<TId> where TId : IEquatable<TId>
{
    string GetName(IFileSystemNodeModel<TId> nodeModel);
}
