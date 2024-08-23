using System;

namespace ProtonDrive.Sync.Shared;

public class EvenIdentitySource : IIdentitySource<long>
{
    private long _lastValue;

    public long NextValue()
    {
        return _lastValue += 2;
    }

    public void InitializeFrom(long value)
    {
        var lastValue = Math.Max(value, _lastValue);
        _lastValue = lastValue & -2L;
    }
}
