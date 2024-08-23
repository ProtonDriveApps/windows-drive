using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Sync.Shared.FileSystem;

public interface IFileHydrationDemandHandler<TId>
    where TId : IEquatable<TId>
{
    Task HandleAsync(IFileHydrationDemand<TId> hydrationDemand, CancellationToken cancellationToken);
}
