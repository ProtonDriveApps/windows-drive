using Proton.Drive.Sdk.Sync.Shared.Trees;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;

namespace Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;

internal class AdapterTree<TId, TAltId> : LooseCompoundAltIdentifiableFileSystemTree<AdapterTree<TId, TAltId>, AdapterTreeNode<TId, TAltId>, AdapterTreeNodeModel<TId, TAltId>, TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    public AdapterTree(
        ILooseCompoundAltIdentifiableTreeNodeRepository<AdapterTreeNodeModel<TId, TAltId>, TId, TAltId> repository,
        IFileSystemNodeFactory<AdapterTree<TId, TAltId>, AdapterTreeNode<TId, TAltId>, AdapterTreeNodeModel<TId, TAltId>, TId> factory)
        : base(repository, factory)
    {
    }
}
