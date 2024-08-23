using System;

namespace ProtonDrive.Sync.Shared;

public class OddIdentitySource : IIdentitySource<long>
{
    private long _lastValue = -1;

    public long NextValue()
    {
        return _lastValue += 2;
    }

    public void InitializeFrom(long value)
    {
        var lastValue = Math.Max(value, _lastValue);
        _lastValue = lastValue - ((lastValue & 1L) ^ 1L);
    }
}
