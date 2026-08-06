using Proton.Drive.Sdk.Sync.Shared.Trees.Operations;

namespace Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

public static class FileSystemTreeOperationsExtensions
{
    public static void Execute<TTree, TNode, TModel, TId>(
        this FileSystemTreeOperations<TTree, TNode, TModel, TId> subject,
        IEnumerable<Operation<TModel>> operations)
        where TTree : FileSystemTree<TTree, TNode, TModel, TId>
        where TNode : FileSystemNode<TTree, TNode, TModel, TId>
        where TModel : FileSystemNodeModel<TId>, new()
        where TId : IEquatable<TId>
    {
        foreach (var operation in operations)
        {
            subject.Execute(operation);
        }
    }
}
