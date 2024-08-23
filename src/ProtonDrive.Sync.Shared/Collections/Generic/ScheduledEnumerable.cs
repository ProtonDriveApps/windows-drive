using System.Collections.Generic;
using System.Threading;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.Sync.Shared.Collections.Generic;

public class ScheduledEnumerable<T> : IAsyncEnumerable<T>
{
    private readonly IScheduler _syncScheduler;
    private readonly IEnumerable<T> _origin;

    public ScheduledEnumerable(IScheduler syncScheduler, IEnumerable<T> origin)
    {
        _syncScheduler = syncScheduler;
        _origin = origin;
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new ScheduledEnumerator<T>(_syncScheduler, _origin, cancellationToken);
    }
}
