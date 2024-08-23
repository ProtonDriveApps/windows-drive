using System;

namespace ProtonDrive.Sync.Shared.Trees;

public interface ILooseCompoundAltIdentifiable<out TId, TAltId> : IAltIdentifiable<TId, LooseCompoundAltIdentity<TAltId>>
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
}
