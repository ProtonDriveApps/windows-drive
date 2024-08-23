using System;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Adapter.OnDemandHydration.FileSizeCorrection;

internal interface IFileSizeCorrector<TId, TAltId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    Task UpdateSizeAsync(AdapterTreeNodeModel<TId, TAltId> initialNodeModel, IFileHydrationDemand<TAltId> hydrationDemand, CancellationToken cancellationToken);
}
