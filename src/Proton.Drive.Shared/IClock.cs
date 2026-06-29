namespace Proton.Drive.Shared;

public interface IClock
{
    TickCount TickCount { get; }
    DateTime UtcNow { get; }
}
