using System;
using ProtonDrive.Shared;

namespace ProtonDrive.Sync.Shared.Trees;

public interface IAltIdentifiable<out TId, out TAltId> : IIdentifiable<TId>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    TAltId AltId { get; }
}
