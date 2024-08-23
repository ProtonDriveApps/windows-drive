using System;
using System.Collections.Generic;

namespace ProtonDrive.Sync.Shared.Trees.Changes;

public interface IReceivedTreeChanges<TId> : IEnumerable<TreeChange<TId>>
    where TId : IEquatable<TId>
{
    event EventHandler Added;

    bool IsEmpty { get; }

    void AcknowledgeConsumed(TreeChange<TId> lastConsumedItem);
}
