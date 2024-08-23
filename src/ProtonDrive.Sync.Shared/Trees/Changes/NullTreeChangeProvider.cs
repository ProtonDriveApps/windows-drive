using System;

namespace ProtonDrive.Sync.Shared.Trees.Changes;

public sealed class NullTreeChangeProvider<TId> : ITreeChangeProvider<TId>
    where TId : IEquatable<TId>
{
    public event EventHandler<TreeChange<TId>> TreeChanged { add { } remove { } }

    public void AcknowledgeConsumed(TreeChange<TId> lastConsumedChange) => throw new NotSupportedException();
}
