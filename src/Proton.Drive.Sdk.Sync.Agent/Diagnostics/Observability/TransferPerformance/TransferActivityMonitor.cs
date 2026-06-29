using System.Collections.Concurrent;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Observability.TransferPerformance;

internal sealed class TransferActivityMonitor<TKey>
    where TKey : IEquatable<TKey>
{
    private readonly ConcurrentDictionary<TKey, Void> _activeTransfers = [];

    public int Add(TKey key)
    {
        var hasBeenAdded = _activeTransfers.TryAdd(key, default);

        return hasBeenAdded ? 1 : 0;
    }

    public int Remove(TKey key)
    {
        var hasBeenRemoved = _activeTransfers.TryRemove(key, out _);

        return hasBeenRemoved ? -1 : 0;
    }

    public void Clear()
    {
        _activeTransfers.Clear();
    }

    private struct Void;
}
