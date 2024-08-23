using System;

namespace ProtonDrive.Shared;

public interface IClock
{
    TickCount TickCount { get; }
    DateTime UtcNow { get; }
}
