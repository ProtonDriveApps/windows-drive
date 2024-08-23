using System;
using System.Threading;

namespace ProtonDrive.Sync.Shared;

public class ConcurrentIdentitySource : IIdentitySource<long>
{
    private long _lastValue;

    public long NextValue()
    {
        return Interlocked.Increment(ref _lastValue);
    }

    public void InitializeFrom(long value)
    {
        _lastValue = Math.Max(value, _lastValue);
    }
}
