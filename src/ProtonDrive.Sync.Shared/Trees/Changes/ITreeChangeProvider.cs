using System;

namespace ProtonDrive.Sync.Shared.Trees.Changes;

public interface ITreeChangeProvider<TId>
    where TId : IEquatable<TId>
{
    event EventHandler<TreeChange<TId>> TreeChanged;
    void AcknowledgeConsumed(TreeChange<TId> lastConsumedItem);
}
