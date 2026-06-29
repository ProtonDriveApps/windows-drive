using Proton.Drive.Shared;

namespace Proton.Drive.Sdk.Sync.Shared.Trees;

public interface IAltIdentifiable<out TId, out TAltId> : IIdentifiable<TId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    TAltId AltId { get; }
}
