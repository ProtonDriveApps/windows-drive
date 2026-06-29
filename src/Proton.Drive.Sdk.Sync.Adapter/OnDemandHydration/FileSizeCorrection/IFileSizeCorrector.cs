using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Adapter.OnDemandHydration.FileSizeCorrection;

internal interface IFileSizeCorrector<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    Task UpdateSizeAsync(AdapterTreeNodeModel<TId, TAltId> initialNodeModel, IFileHydrationDemand<TAltId> hydrationDemand, CancellationToken cancellationToken);
}
