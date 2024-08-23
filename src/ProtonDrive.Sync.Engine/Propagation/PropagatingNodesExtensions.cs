using System;
using System.Threading.Tasks;

namespace ProtonDrive.Sync.Engine.Propagation;

internal static class PropagatingNodesExtensions
{
    public static async Task<(bool Success, TResult? Result)> LockNodeAndExecute<TId, TResult>(
        this PropagatingNodes<TId> propagatingNodes,
        TId id,
        Func<Task<TResult>> origin)
        where TId : IEquatable<TId>
    {
        IDisposable? disposableLock = null;
        try
        {
            if (!propagatingNodes.TryLock(id, out disposableLock))
            {
                return (false, default);
            }

            return (true, await origin.Invoke().ConfigureAwait(false));
        }
        finally
        {
            disposableLock?.Dispose();
        }
    }
}
