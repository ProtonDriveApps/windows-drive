using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.Sync.Shared.Collections.Generic;

internal class ScheduledEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IScheduler _syncScheduler;
    private readonly IEnumerable<T> _origin;
    private readonly CancellationToken _cancellationToken;

    private IEnumerator<T>? _enumerator;

    public ScheduledEnumerator(IScheduler syncScheduler, IEnumerable<T> origin, CancellationToken cancellationToken)
    {
        _syncScheduler = syncScheduler;
        _origin = origin;
        _cancellationToken = cancellationToken;
    }

    public T Current => _enumerator!.Current;

    public async ValueTask<bool> MoveNextAsync()
    {
        _enumerator ??= _origin.GetEnumerator();

        return await Schedule(MoveNext).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        _enumerator?.Dispose();
        _enumerator = null;

        return default;
    }

    private bool MoveNext()
    {
        _cancellationToken.ThrowIfCancellationRequested();

        return _enumerator!.MoveNext();
    }

    private Task<TResult> Schedule<TResult>(Func<TResult> origin)
    {
        return _syncScheduler.Schedule(origin);
    }
}
