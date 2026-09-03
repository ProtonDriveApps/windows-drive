namespace Proton.Drive.Shared;

public class SystemClock : IClock
{
    public TickCount TickCount => TickCount.Current;
    public DateTime UtcNow => DateTime.UtcNow;

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.Delay(delay, cancellationToken);
}
