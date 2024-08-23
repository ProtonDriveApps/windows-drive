using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ProtonDrive.Sync.Engine.Propagation;

internal sealed class PropagatingNodes<TId>
    where TId : IEquatable<TId>
{
    private readonly ConcurrentDictionary<TId, Void> _propagatingNodes = new();

    public bool TryLock(TId nodeId, [NotNullWhen(true)] out IDisposable? disposableLock)
    {
        if (!_propagatingNodes.TryAdd(nodeId, default))
        {
            disposableLock = default;
            return false;
        }

        disposableLock = new DisposableLock<TId>(nodeId, Unlock);
        return true;
    }

    private void Unlock(TId nodeId)
    {
        _propagatingNodes.TryRemove(nodeId, out _);
    }

    private struct Void { }

    private sealed class DisposableLock<T> : IDisposable
    {
        private readonly T _state;
        private readonly Action<T> _onUnlock;

        public DisposableLock(T state, Action<T> onUnlock)
        {
            _state = state;
            _onUnlock = onUnlock;
        }

        public void Dispose()
        {
            _onUnlock(_state);
        }
    }
}
