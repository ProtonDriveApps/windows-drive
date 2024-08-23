using System;

namespace ProtonDrive.Shared;

public class SystemClock : IClock
{
    public TickCount TickCount => TickCount.Current;
    public DateTime UtcNow => DateTime.UtcNow;
}
