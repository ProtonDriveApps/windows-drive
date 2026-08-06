namespace Proton.Drive.Sdk.Sync.Shared.Trees.Changes;

public interface ITreeChangeProvider<TId>
    where TId : IEquatable<TId>
{
    event EventHandler<TreeChange<TId>> TreeChanged;
    void AcknowledgeConsumed(TreeChange<TId> lastConsumedItem);
}
