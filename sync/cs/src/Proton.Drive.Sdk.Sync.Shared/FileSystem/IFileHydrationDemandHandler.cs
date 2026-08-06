namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

public interface IFileHydrationDemandHandler<TId>
    where TId : IEquatable<TId>
{
    Task HandleAsync(IFileHydrationDemand<TId> hydrationDemand, CancellationToken cancellationToken);
}
